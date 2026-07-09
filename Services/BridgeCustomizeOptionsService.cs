// © DeadlyDeathAngel.
// Licensed under the MIT license.

namespace LuminusBridge.Services;

using LuminusBridge.Api;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;

/// <summary>
/// Resolves hair, face paint, eyebrow, and facial-feature options from game Excel data.
/// </summary>
public sealed class BridgeCustomizeOptionsService
{
	private const uint HairOptionsLength = 130;
	private const uint FacePaintOptionsLength = 50;

	private readonly IDataManager dataManager;
	private readonly IPluginLog log;

	private readonly Dictionary<(string Kind, byte Tribe, byte Gender, byte Key4), List<CustomizeOptionDto>> cache = [];

	public BridgeCustomizeOptionsService(IDataManager dataManager, IPluginLog log)
	{
		this.dataManager = dataManager;
		this.log = log;
	}

	public IReadOnlyList<CustomizeOptionDto> GetOptions(string kind, byte tribe, byte gender, byte face = 1, byte age = 1)
	{
		string normalized = NormalizeKind(kind);
		byte key4 = normalized switch
		{
			"facialfeatures" => face,
			"tailears" => age == 0 ? (byte)1 : age,
			_ => 0,
		};
		var key = (normalized, tribe, gender, key4);
		if (this.cache.TryGetValue(key, out List<CustomizeOptionDto>? cached))
		{
			return cached;
		}

		List<CustomizeOptionDto> built = normalized switch
		{
			"hair" => this.BuildHairOptions(tribe, gender),
			"facepaint" => this.BuildFacePaintOptions(tribe, gender),
			"eyebrows" => this.BuildEyebrowOptions(tribe, gender),
			"facialfeatures" => this.BuildFacialFeatureOptions(tribe, gender, face),
			"tailears" => this.BuildTailEarsOptions(tribe, gender, age),
			_ => [],
		};

		built.Sort((a, b) => a.Id.CompareTo(b.Id));
		this.cache[key] = built;
		return built;
	}

	private List<CustomizeOptionDto> BuildHairOptions(byte tribe, byte gender)
	{
		if (!TryGetFeatureStartIndex("hair", tribe, gender, out uint start))
		{
			return [];
		}

		return this.ReadCharaMakeFeatures(start, HairOptionsLength, includeZeroId: false);
	}

	private List<CustomizeOptionDto> BuildFacePaintOptions(byte tribe, byte gender)
	{
		if (!TryGetFeatureStartIndex("facepaint", tribe, gender, out uint start))
		{
			return [];
		}

		var options = this.ReadCharaMakeFeatures(start, FacePaintOptionsLength, includeZeroId: true);
		if (options.Count == 0 || options[0].Id != 0)
		{
			options.Insert(0, new CustomizeOptionDto
			{
				Id = 0,
				Name = "(None)",
				IconId = 0,
			});
		}

		return options;
	}

	private List<CustomizeOptionDto> BuildEyebrowOptions(byte tribe, byte gender)
	{
		byte min = 0;
		byte max = 8;
		if (TryGetCustomizeRange(tribe, gender, customizeIndex: 14, out byte rangeMin, out byte rangeMax))
		{
			min = rangeMin;
			max = rangeMax;
		}

		var options = new List<CustomizeOptionDto>();
		for (byte id = min; id <= max; id++)
		{
			options.Add(new CustomizeOptionDto
			{
				Id = id,
				Name = $"Eyebrow {id + 1}",
				IconId = 0,
			});
		}

		return options;
	}

	private List<CustomizeOptionDto> BuildTailEarsOptions(byte tribe, byte gender, byte age)
	{
		byte resolvedAge = age == 0 ? (byte)1 : age;
		short? dataPath = BridgeDataPathResolver.ToDataPath(tribe, gender, resolvedAge);
		if (!dataPath.HasValue)
		{
			return [];
		}

		bool isViera = tribe is 15 or 16;
		string label = isViera ? "Ear" : "Tail";
		var options = new List<CustomizeOptionDto>();
		var variants = new List<short> { dataPath.Value };
		if (resolvedAge == 1)
		{
			variants.Add((short)(dataPath.Value + 3));
		}

		for (ushort id = 0; id < byte.MaxValue; id++)
		{
			foreach (short variant in variants)
			{
				string path = BridgeDataPathResolver.ResolveTailEarsPath(variant, id);
				if (!this.FileExists(path))
				{
					continue;
				}

				if (options.Exists(o => o.Id == id))
				{
					break;
				}

				options.Add(new CustomizeOptionDto
				{
					Id = (byte)id,
					Name = $"{label} {id}",
					IconId = 0,
				});
				break;
			}
		}

		return options;
	}

	private bool FileExists(string path)
	{
		try
		{
			return this.dataManager.GetFile(path) != null;
		}
		catch
		{
			return false;
		}
	}

	private List<CustomizeOptionDto> BuildFacialFeatureOptions(byte tribe, byte gender, byte face)
	{
		var options = new List<CustomizeOptionDto>
		{
			new()
			{
				Id = 0,
				Name = "(None)",
				IconId = 0,
			},
		};

		if (!TryGetFacialFeatureIcons(tribe, gender, face, out List<uint> icons))
		{
			for (byte i = 0; i < 7; i++)
			{
				options.Add(new CustomizeOptionDto
				{
					Id = FacialFeatureFlag(i),
					Name = $"Feature {i + 1}",
					IconId = 0,
				});
			}

			options.Add(new CustomizeOptionDto
			{
				Id = 128,
				Name = "Legacy tattoo",
				IconId = 0,
			});
			return options;
		}

		for (byte i = 0; i < icons.Count && i < 7; i++)
		{
			options.Add(new CustomizeOptionDto
			{
				Id = FacialFeatureFlag(i),
				Name = $"Feature {i + 1}",
				IconId = icons[i],
			});
		}

		options.Add(new CustomizeOptionDto
		{
			Id = 128,
			Name = "Legacy tattoo",
			IconId = 0,
		});

		return options;
	}

	private List<CustomizeOptionDto> ReadCharaMakeFeatures(uint startIndex, uint count, bool includeZeroId)
	{
		var options = new List<CustomizeOptionDto>();
		try
		{
			var sheet = this.dataManager.Excel.GetSheet<CharaMakeCustomize>();
			for (uint rowIndex = startIndex; rowIndex < startIndex + count; rowIndex++)
			{
				CharaMakeCustomize row = sheet.GetRow(rowIndex);
				if (row.RowId == 0)
				{
					continue;
				}

				byte featureId = row.FeatureID;
				if (!includeZeroId && featureId == 0)
				{
					continue;
				}

				string name = ResolveFeatureName(row);
				uint iconId = ResolveFeatureIcon(row);
				options.Add(new CustomizeOptionDto
				{
					Id = featureId,
					Name = name,
					IconId = iconId,
				});
			}
		}
		catch (Exception ex)
		{
			this.log.Warning(ex, "Failed to read CharaMakeCustomize options.");
		}

		return options;
	}

	private static string ResolveFeatureName(CharaMakeCustomize row)
	{
		Item hint = row.HintItem.Value;
		if (hint.RowId != 0)
		{
			string name = hint.Name.ToString();
			if (!string.IsNullOrWhiteSpace(name))
			{
				return name;
			}
		}

		return row.FeatureID == 0 ? "(None)" : $"Style {row.FeatureID}";
	}

	private static uint ResolveFeatureIcon(CharaMakeCustomize row)
	{
		return row.Icon != 0 ? (uint)row.Icon : 0;
	}

	private bool TryGetCustomizeRange(byte tribe, byte gender, byte customizeIndex, out byte min, out byte max)
	{
		min = 0;
		max = 8;
		try
		{
			var sheet = this.dataManager.Excel.GetSheet<CharaMakeType>();
			foreach (CharaMakeType row in sheet)
			{
				if (row.RowId == 0)
				{
					continue;
				}

				if (row.Tribe.RowId == 0)
				{
					continue;
				}

				byte rowTribe = (byte)row.Tribe.RowId;
				byte rowGender = (byte)row.Gender;
				if (rowTribe != tribe || rowGender != gender)
				{
					continue;
				}

				foreach (CharaMakeType.CharaMakeStructStruct entry in row.CharaMakeStruct)
				{
					if ((byte)entry.Customize != customizeIndex)
					{
						continue;
					}

					byte subMenuType = entry.SubMenuType;
					if (subMenuType is not 0x00 and not 0x01 and not 0x04)
					{
						continue;
					}

					min = entry.SubMenuGraphic[0];
					max = (byte)(entry.SubMenuNum - 1 + min);
					return true;
				}
			}
		}
		catch (Exception ex)
		{
			this.log.Warning(ex, "Failed to read eyebrow customize range.");
		}

		return false;
	}

	private bool TryGetFacialFeatureIcons(byte tribe, byte gender, byte face, out List<uint> icons)
	{
		icons = [];
		try
		{
			var sheet = this.dataManager.Excel.GetSheet<CharaMakeType>();
			foreach (CharaMakeType row in sheet)
			{
				if (row.RowId == 0 || row.Tribe.RowId == 0)
				{
					continue;
				}

				byte rowTribe = (byte)row.Tribe.RowId;
				byte rowGender = (byte)row.Gender;
				if (rowTribe != tribe || rowGender != gender)
				{
					continue;
				}

				int hrothOffset = 0;
				bool isHroth = tribe is 13 or 14;
				if (isHroth && (gender == 1 || (face >= 5 && face <= 8)))
				{
					hrothOffset = 4;
				}

				var allIcons = new List<uint>();
				foreach (CharaMakeType.FacialFeatureOptionStruct option in row.FacialFeatureOption)
				{
					allIcons.Add((uint)option.Option1);
					allIcons.Add((uint)option.Option2);
					allIcons.Add((uint)option.Option3);
					allIcons.Add((uint)option.Option4);
					allIcons.Add((uint)option.Option5);
					allIcons.Add((uint)option.Option6);
					allIcons.Add((uint)option.Option7);
				}

				for (byte i = 0; i < 7; i++)
				{
					int id = ((face - (1 + hrothOffset)) * 7) + i;
					if (id < 0 || id >= allIcons.Count)
					{
						continue;
					}

					icons.Add(allIcons[id]);
				}

				return icons.Count > 0;
			}
		}
		catch (Exception ex)
		{
			this.log.Warning(ex, "Failed to read facial feature icons.");
		}

		return false;
	}

	private static byte FacialFeatureFlag(int index) => index switch
	{
		0 => 1,
		1 => 2,
		2 => 4,
		3 => 8,
		4 => 16,
		5 => 32,
		6 => 64,
		_ => 0,
	};

	private static string NormalizeKind(string kind)
		=> kind.Trim().ToLowerInvariant().Replace("_", string.Empty).Replace("-", string.Empty);

	private static bool TryGetFeatureStartIndex(string kind, byte tribe, byte gender, out uint start)
	{
		start = 0;
		if (!FeatureStartIndexMap.TryGetValue((kind, tribe, gender), out uint value))
		{
			return false;
		}

		start = value;
		return true;
	}

	private static readonly Dictionary<(string Kind, byte Tribe, byte Gender), uint> FeatureStartIndexMap = new()
	{
		// Hyur
		{ ("hair", 1, 0), 0 }, { ("hair", 1, 1), 130 },
		{ ("hair", 2, 0), 260 }, { ("hair", 2, 1), 390 },
		{ ("facepaint", 1, 0), 2400 }, { ("facepaint", 1, 1), 2450 },
		{ ("facepaint", 2, 0), 2500 }, { ("facepaint", 2, 1), 2550 },
		// Elezen
		{ ("hair", 3, 0), 520 }, { ("hair", 3, 1), 650 },
		{ ("hair", 4, 0), 520 }, { ("hair", 4, 1), 650 },
		{ ("facepaint", 3, 0), 2600 }, { ("facepaint", 3, 1), 2650 },
		{ ("facepaint", 4, 0), 2700 }, { ("facepaint", 4, 1), 2750 },
		// Lalafell
		{ ("hair", 5, 0), 780 }, { ("hair", 5, 1), 910 },
		{ ("hair", 6, 0), 780 }, { ("hair", 6, 1), 910 },
		{ ("facepaint", 5, 0), 2800 }, { ("facepaint", 5, 1), 2850 },
		{ ("facepaint", 6, 0), 2900 }, { ("facepaint", 6, 1), 2950 },
		// Miqo'te
		{ ("hair", 7, 0), 1040 }, { ("hair", 7, 1), 1170 },
		{ ("hair", 8, 0), 1040 }, { ("hair", 8, 1), 1170 },
		{ ("facepaint", 7, 0), 3000 }, { ("facepaint", 7, 1), 3050 },
		{ ("facepaint", 8, 0), 3100 }, { ("facepaint", 8, 1), 3150 },
		// Roegadyn
		{ ("hair", 9, 0), 1300 }, { ("hair", 9, 1), 1430 },
		{ ("hair", 10, 0), 1300 }, { ("hair", 10, 1), 1430 },
		{ ("facepaint", 9, 0), 3200 }, { ("facepaint", 9, 1), 3250 },
		{ ("facepaint", 10, 0), 3300 }, { ("facepaint", 10, 1), 3350 },
		// Au Ra
		{ ("hair", 11, 0), 1560 }, { ("hair", 11, 1), 1690 },
		{ ("hair", 12, 0), 1560 }, { ("hair", 12, 1), 1690 },
		{ ("facepaint", 11, 0), 3400 }, { ("facepaint", 11, 1), 3450 },
		{ ("facepaint", 12, 0), 3500 }, { ("facepaint", 12, 1), 3550 },
		// Hrothgar
		{ ("hair", 13, 0), 1820 }, { ("hair", 13, 1), 1950 },
		{ ("hair", 14, 0), 1820 }, { ("hair", 14, 1), 1950 },
		{ ("facepaint", 13, 0), 3600 }, { ("facepaint", 13, 1), 3650 },
		{ ("facepaint", 14, 0), 3700 }, { ("facepaint", 14, 1), 3750 },
		// Viera
		{ ("hair", 15, 0), 2080 }, { ("hair", 15, 1), 2210 },
		{ ("hair", 16, 0), 2080 }, { ("hair", 16, 1), 2210 },
		{ ("facepaint", 15, 0), 3800 }, { ("facepaint", 15, 1), 3850 },
		{ ("facepaint", 16, 0), 3900 }, { ("facepaint", 16, 1), 3950 },
	};
}
