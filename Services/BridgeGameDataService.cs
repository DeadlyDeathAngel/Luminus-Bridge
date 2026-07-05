// © DeadlyDeathAngel.
// Licensed under the MIT license.

namespace AnamnesisBridge.Services;

using AnamnesisBridge.Api;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Resolves item/dye names and customize colour palettes from game Excel + human.cmp.
/// </summary>
public sealed class BridgeGameDataService
{
	private readonly IDataManager dataManager;
	private readonly IPluginLog log;
	private readonly object gate = new();

	private bool itemsBuilt;
	private bool dyesBuilt;
	private bool colorsBuilt;

	private readonly Dictionary<string, List<CatalogItemDto>> itemsBySlot = new(StringComparer.OrdinalIgnoreCase);
	/// <summary>Model key → all items sharing that model (many items collide; resolve by slot).</summary>
	private readonly Dictionary<(ushort Set, ushort Base, ushort Variant), List<CatalogItemDto>> itemsByModel = [];
	private readonly List<DyeDto> dyes = [];
	private readonly Dictionary<byte, DyeDto> dyesById = [];
	private ColorEntryDto[]? allColors;

	public BridgeGameDataService(IDataManager dataManager, IPluginLog log)
	{
		this.dataManager = dataManager;
		this.log = log;
	}

	public IReadOnlyList<DyeDto> GetDyes()
	{
		this.EnsureDyes();
		return this.dyes;
	}

	public IReadOnlyList<CatalogItemDto> GetItemsForSlot(string slot)
	{
		this.EnsureItems();
		return this.itemsBySlot.TryGetValue(NormalizeSlot(slot), out List<CatalogItemDto>? list)
			? list
			: [];
	}

	public IReadOnlyList<ColorEntryDto> GetColors(string type, byte tribe, byte gender)
	{
		this.EnsureColors();
		if (this.allColors == null || this.allColors.Length == 0)
		{
			return [];
		}

		return type.ToLowerInvariant() switch
		{
			"skin" => SliceUnique(tribe, gender, paletteIndex: 3, count: 192),
			"hair" => SliceUnique(tribe, gender, paletteIndex: 4, count: 208),
			"highlight" or "hairhighlights" => SliceShared(paletteIndex: 1, count: 192),
			"eye" or "eyes" => SliceShared(paletteIndex: 0, count: 192),
			"facialfeature" => SliceShared(paletteIndex: 0, count: 192),
			"facepaint" => SliceSplit(paletteIndex: 13, count: 96),
			"lips" => SliceSplit(paletteIndex: 13, count: 96),
			_ => [],
		};
	}

	public void ResolveGear(
		EquipmentSlotDto slot,
		out string itemName,
		out string dyeName,
		out string dye2Name,
		out string dyeHex,
		out string dye2Hex,
		out uint iconId)
	{
		this.EnsureItems();
		this.EnsureDyes();

		CatalogItemDto? item = this.LookupItem(0, slot.Set, slot.Variant, slot.Slot);
		itemName = item?.Name ?? (slot.Set == 0 && slot.Variant == 0 ? "(None)" : $"Model {slot.Set}/{slot.Variant}");
		iconId = item?.IconId ?? 0;
		(dyeName, dyeHex) = this.LookupDye(slot.Dye);
		(dye2Name, dye2Hex) = this.LookupDye(slot.Dye2);
	}

	public void ResolveWeapon(
		WeaponSlotDto weapon,
		out string itemName,
		out string dyeName,
		out string dye2Name,
		out string dyeHex,
		out string dye2Hex,
		out uint iconId)
	{
		this.EnsureItems();
		this.EnsureDyes();

		CatalogItemDto? item = this.LookupItem(weapon.Set, weapon.Base, weapon.Variant, weapon.Slot);
		itemName = item?.Name
			?? (weapon.Set == 0 && weapon.Base == 0 ? "(None)" : $"Weapon {weapon.Set}/{weapon.Base}/{weapon.Variant}");
		iconId = item?.IconId ?? 0;
		(dyeName, dyeHex) = this.LookupDye(weapon.Dye);
		(dye2Name, dye2Hex) = this.LookupDye(weapon.Dye2);
	}

	public byte[]? TryGetIconBmp(uint iconId)
	{
		if (iconId == 0)
		{
			return null;
		}

		try
		{
			uint folder = iconId / 1000 * 1000;
			string[] paths =
			[
				$"ui/icon/{folder:D6}/{iconId:D6}_hr1.tex",
				$"ui/icon/{folder:D6}/{iconId:D6}.tex",
			];

			foreach (string path in paths)
			{
				var file = this.dataManager.GetFile<Lumina.Data.Files.TexFile>(path);
				if (file == null)
				{
					continue;
				}

				byte[]? image = file.ImageData;
				if (image == null || image.Length == 0)
				{
					continue;
				}

				int width = file.Header.Width;
				int height = file.Header.Height;
				return BgraToBmp(image, width, height);
			}
		}
		catch (Exception ex)
		{
			this.log.Warning(ex, "Failed to load icon {IconId}", iconId);
		}

		return null;
	}

	private CatalogItemDto? LookupItem(ushort set, ushort modelBase, ushort variant, string slot)
	{
		if (modelBase == 0 && set == 0)
		{
			return null;
		}

		string normSlot = NormalizeSlot(slot);

		// 1) Exact model + correct equip slot (critical: many items share models).
		if (this.itemsByModel.TryGetValue((set, modelBase, variant), out List<CatalogItemDto>? exact))
		{
			CatalogItemDto? fit = exact.FirstOrDefault(i => this.ItemFitsSlot(i, normSlot));
			if (fit != null)
			{
				return fit;
			}
		}

		// 2) Gear stores variant as a byte on EquipmentModelId — try low byte only.
		ushort lowVariant = (byte)variant;
		if (lowVariant != variant
			&& this.itemsByModel.TryGetValue((set, modelBase, lowVariant), out List<CatalogItemDto>? lowExact))
		{
			CatalogItemDto? fit = lowExact.FirstOrDefault(i => this.ItemFitsSlot(i, normSlot));
			if (fit != null)
			{
				return fit;
			}
		}

		// 3) Same base within this slot, any variant.
		foreach (CatalogItemDto candidate in this.GetItemsForSlot(normSlot))
		{
			if (candidate.ItemId == 0)
			{
				continue;
			}

			if (candidate.Set == set && candidate.Base == modelBase
				&& (candidate.Variant == variant || (byte)candidate.Variant == (byte)variant))
			{
				return candidate;
			}
		}

		foreach (CatalogItemDto candidate in this.GetItemsForSlot(normSlot))
		{
			if (candidate.ItemId != 0 && candidate.Set == set && candidate.Base == modelBase)
			{
				return candidate;
			}
		}

		return null;
	}

	private bool ItemFitsSlot(CatalogItemDto item, string normSlot)
	{
		if (!this.itemsBySlot.TryGetValue(normSlot, out List<CatalogItemDto>? list))
		{
			return false;
		}

		foreach (CatalogItemDto entry in list)
		{
			if (entry.ItemId == item.ItemId)
			{
				return true;
			}
		}

		return false;
	}

	private static byte[] BgraToBmp(byte[] bgra, int width, int height)
	{
		int rowStride = ((width * 3 + 3) / 4) * 4;
		int pixelBytes = rowStride * height;
		int fileSize = 54 + pixelBytes;
		byte[] bmp = new byte[fileSize];
		bmp[0] = (byte)'B';
		bmp[1] = (byte)'M';
		BitConverter.TryWriteBytes(bmp.AsSpan(2, 4), fileSize);
		BitConverter.TryWriteBytes(bmp.AsSpan(10, 4), 54);
		BitConverter.TryWriteBytes(bmp.AsSpan(14, 4), 40);
		BitConverter.TryWriteBytes(bmp.AsSpan(18, 4), width);
		BitConverter.TryWriteBytes(bmp.AsSpan(22, 4), height);
		BitConverter.TryWriteBytes(bmp.AsSpan(26, 2), (short)1);
		BitConverter.TryWriteBytes(bmp.AsSpan(28, 2), (short)24);

		for (int y = 0; y < height; y++)
		{
			int srcRow = y * width * 4;
			int dstRow = 54 + ((height - 1 - y) * rowStride);
			for (int x = 0; x < width; x++)
			{
				int s = srcRow + (x * 4);
				int d = dstRow + (x * 3);
				bmp[d] = bgra[s];
				bmp[d + 1] = bgra[s + 1];
				bmp[d + 2] = bgra[s + 2];
			}
		}

		return bmp;
	}

	private (string Name, string Hex) LookupDye(byte id)
	{
		if (id == 0)
		{
			return ("(None)", "#00000000");
		}

		if (this.dyesById.TryGetValue(id, out DyeDto? dye))
		{
			return (dye.Name, dye.Hex);
		}

		return ($"Dye {id}", "#808080");
	}

	private void EnsureItems()
	{
		lock (this.gate)
		{
			if (this.itemsBuilt)
			{
				return;
			}

			try
			{
				var sheet = this.dataManager.Excel.GetSheet<Item>();
				var none = new CatalogItemDto { ItemId = 0, Name = "(None)", Set = 0, Base = 0, Variant = 0 };
				foreach (string slot in AllSlots())
				{
					this.itemsBySlot[slot] = [none];
				}

				foreach (Item item in sheet)
				{
					if (item.RowId == 0)
					{
						continue;
					}

					string name = item.Name.ToString();
					if (string.IsNullOrWhiteSpace(name))
					{
						continue;
					}

					ulong modelMain = item.ModelMain;
					if (modelMain == 0)
					{
						continue;
					}

					bool isWeapon = item.ItemUICategory.RowId is >= 1 and <= 34
						|| item.EquipSlotCategory.Value.MainHand == 1
						|| item.EquipSlotCategory.Value.OffHand == 1;

					ushort set;
					ushort modelBase;
					ushort variant;
					if (isWeapon)
					{
						set = (ushort)modelMain;
						modelBase = (ushort)(modelMain >> 16);
						variant = (ushort)(modelMain >> 32);
					}
					else
					{
						set = 0;
						modelBase = (ushort)modelMain;
						// EquipmentModelId.Variant is a byte — index with the low byte so reads match.
						variant = (byte)(modelMain >> 16);
					}

					var dto = new CatalogItemDto
					{
						ItemId = item.RowId,
						Name = name,
						Set = set,
						Base = modelBase,
						Variant = variant,
						IconId = item.Icon,
					};

					var key = (set, modelBase, variant);
					if (!this.itemsByModel.TryGetValue(key, out List<CatalogItemDto>? modelList))
					{
						modelList = [];
						this.itemsByModel[key] = modelList;
					}

					modelList.Add(dto);

					foreach (string slot in SlotsForItem(item))
					{
						if (!this.itemsBySlot.TryGetValue(slot, out List<CatalogItemDto>? list))
						{
							list = [none];
							this.itemsBySlot[slot] = list;
						}

						list.Add(dto);
					}
				}

				foreach (List<CatalogItemDto> list in this.itemsBySlot.Values)
				{
					list.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
				}

				this.itemsBuilt = true;
				this.log.Information("Bridge game data: indexed {Count} item models.", this.itemsByModel.Count);
			}
			catch (Exception ex)
			{
				this.log.Error(ex, "Failed to index item sheet.");
				this.itemsBuilt = true;
			}
		}
	}

	private void EnsureDyes()
	{
		lock (this.gate)
		{
			if (this.dyesBuilt)
			{
				return;
			}

			try
			{
				this.dyes.Add(new DyeDto { Id = 0, Name = "(None)", Hex = "#00000000" });
				var sheet = this.dataManager.Excel.GetSheet<Stain>();
				foreach (Stain stain in sheet)
				{
					if (stain.RowId == 0)
					{
						continue;
					}

					string name = stain.Name.ToString();
					if (string.IsNullOrWhiteSpace(name))
					{
						name = $"Dye {stain.RowId}";
					}

					// Stain.Color is BGRA uint in Lumina.
					uint color = stain.Color;
					byte b = (byte)(color & 0xFF);
					byte g = (byte)((color >> 8) & 0xFF);
					byte r = (byte)((color >> 16) & 0xFF);
					var dto = new DyeDto
					{
						Id = (byte)Math.Min(stain.RowId, 255),
						Name = name,
						Hex = $"#{r:X2}{g:X2}{b:X2}",
					};
					this.dyes.Add(dto);
					this.dyesById[dto.Id] = dto;
				}

				this.dyesBuilt = true;
			}
			catch (Exception ex)
			{
				this.log.Error(ex, "Failed to index stain sheet.");
				this.dyesBuilt = true;
			}
		}
	}

	private void EnsureColors()
	{
		lock (this.gate)
		{
			if (this.colorsBuilt)
			{
				return;
			}

			try
			{
				var file = this.dataManager.GetFile("chara/xls/charamake/human.cmp");
				if (file?.Data == null || file.Data.Length < 4)
				{
					this.allColors = [];
					this.colorsBuilt = true;
					return;
				}

				byte[] buffer = file.Data;
				var colors = new List<ColorEntryDto>(buffer.Length / 4);
				for (int at = 0; at + 3 < buffer.Length; at += 4)
				{
					byte r = buffer[at];
					byte g = buffer[at + 1];
					byte b = buffer[at + 2];
					colors.Add(new ColorEntryDto
					{
						Index = (ushort)(colors.Count),
						Hex = $"#{r:X2}{g:X2}{b:X2}",
						R = r,
						G = g,
						B = b,
					});
				}

				this.allColors = colors.ToArray();
				this.colorsBuilt = true;
				this.log.Information("Bridge game data: loaded {Count} customize colours.", this.allColors.Length);
			}
			catch (Exception ex)
			{
				this.log.Error(ex, "Failed to read human.cmp colour data.");
				this.allColors = [];
				this.colorsBuilt = true;
			}
		}
	}

	private IReadOnlyList<ColorEntryDto> SliceUnique(byte tribe, byte gender, int paletteIndex, int count)
	{
		const int uniqueBaseIndex = 0x4800 / 4;
		const int chunkColorsSize = 0x1400 / 4;
		const int colorsPerPalette = 0x400 / 4;
		int tribeGenderIndex = Math.Max(0, ((Math.Max(tribe, (byte)1) - 1) * 2) + gender);
		int start = uniqueBaseIndex + (tribeGenderIndex * chunkColorsSize) + (paletteIndex * colorsPerPalette);
		return this.Slice(start, count);
	}

	private IReadOnlyList<ColorEntryDto> SliceShared(int paletteIndex, int count)
	{
		const int colorsPerPalette = 0x400 / 4;
		return this.Slice(paletteIndex * colorsPerPalette, count);
	}

	private IReadOnlyList<ColorEntryDto> SliceSplit(int paletteIndex, int count)
	{
		const int colorsPerPalette = 0x400 / 4;
		int paintBase = paletteIndex * colorsPerPalette;
		var list = new List<ColorEntryDto>((count * 2) + 32);
		list.AddRange(this.Slice(paintBase, count));
		for (int i = 0; i < 32; i++)
		{
			list.Add(new ColorEntryDto { Index = (ushort)list.Count, Hex = "#00000000", Skip = true });
		}

		list.AddRange(this.Slice(paintBase + (colorsPerPalette / 2), count));
		// Re-index for palette selection (customize byte is palette index, not absolute).
		for (int i = 0; i < list.Count; i++)
		{
			ColorEntryDto entry = list[i];
			list[i] = new ColorEntryDto
			{
				Index = (ushort)i,
				Hex = entry.Hex,
				R = entry.R,
				G = entry.G,
				B = entry.B,
				Skip = entry.Skip,
			};
		}

		return list;
	}

	private IReadOnlyList<ColorEntryDto> Slice(int from, int count)
	{
		if (this.allColors == null || this.allColors.Length <= from)
		{
			return [];
		}

		int actual = Math.Min(count, this.allColors.Length - from);
		var result = new ColorEntryDto[actual];
		for (int i = 0; i < actual; i++)
		{
			ColorEntryDto source = this.allColors[from + i];
			result[i] = new ColorEntryDto
			{
				Index = (ushort)i,
				Hex = source.Hex,
				R = source.R,
				G = source.G,
				B = source.B,
			};
		}

		return result;
	}

	private static IEnumerable<string> AllSlots()
		=>
		[
			"head", "body", "hands", "legs", "feet",
			"ears", "neck", "wrists", "ringRight", "ringLeft",
			"mainHand", "offHand",
		];

	private static string NormalizeSlot(string slot)
		=> slot.Trim() switch
		{
			"ringright" or "rfinger" => "ringRight",
			"ringleft" or "lfinger" => "ringLeft",
			"mainhand" or "mh" => "mainHand",
			"offhand" or "oh" => "offHand",
			_ => slot.Trim(),
		};

	private static IEnumerable<string>SlotsForItem(Item item)
	{
		var cat = item.EquipSlotCategory.Value;
		if (cat.MainHand == 1)
		{
			yield return "mainHand";
		}

		if (cat.OffHand == 1)
		{
			yield return "offHand";
		}

		if (cat.Head == 1)
		{
			yield return "head";
		}

		if (cat.Body == 1)
		{
			yield return "body";
		}

		if (cat.Gloves == 1)
		{
			yield return "hands";
		}

		if (cat.Legs == 1)
		{
			yield return "legs";
		}

		if (cat.Feet == 1)
		{
			yield return "feet";
		}

		if (cat.Ears == 1)
		{
			yield return "ears";
		}

		if (cat.Neck == 1)
		{
			yield return "neck";
		}

		if (cat.Wrists == 1)
		{
			yield return "wrists";
		}

		if (cat.FingerR == 1)
		{
			yield return "ringRight";
		}

		if (cat.FingerL == 1)
		{
			yield return "ringLeft";
		}
	}
}
