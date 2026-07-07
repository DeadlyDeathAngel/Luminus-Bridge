// © DeadlyDeathAngel.
// Licensed under the MIT license.

namespace AnamnesisBridge.GameData;

using System;
using System.Collections.Generic;
using AnamnesisBridge.Api;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

internal static class BridgeCatalogExtras
{
	private const int StandardCategory = 0;
	private const int PerformanceCategory = 1 << 5;

	public const uint NpcBodyItemId = 0xE000_0001;
	public const uint InvisibleBodyItemId = 0xE000_0002;
	public const uint InvisibleHeadItemId = 0xE000_0003;
	public const uint PerformItemIdBase = 0xA000_0000;

	public static IEnumerable<(CatalogItemDto Item, IReadOnlyList<string> Slots)> SpecialItems()
	{
		yield return (CreateGear(
			NpcBodyItemId,
			"NPC Smallclothes",
			"Smallclothes worn by NPCs.",
			9903,
			1,
			StandardCategory), ["body", "hands", "legs", "feet"]);

		yield return (CreateGear(
			InvisibleBodyItemId,
			"Invisible Body",
			"Hides body gear.",
			6121,
			254,
			StandardCategory), ["body"]);

		yield return (CreateGear(
			InvisibleHeadItemId,
			"Invisible Head",
			"Hides head gear.",
			6121,
			254,
			StandardCategory), ["head"]);
	}

	public static IEnumerable<(CatalogItemDto Item, IReadOnlyList<string> Slots)> PerformItems(IDataManager dataManager)
	{
		var sheet = dataManager.Excel.GetSheet<Perform>();
		foreach (Perform row in sheet)
		{
			if (row.RowId == 0)
			{
				continue;
			}

			string name = row.Name.ToString();
			if (string.IsNullOrWhiteSpace(name))
			{
				continue;
			}

			ulong model = row.ModelKey;
			if (model == 0)
			{
				continue;
			}

			ushort set = (ushort)model;
			ushort modelBase = (ushort)(model >> 16);
			ushort variant = (ushort)(model >> 32);
			var item = new CatalogItemDto
			{
				ItemId = PerformItemIdBase + row.RowId,
				Name = name,
				Set = set,
				Base = modelBase,
				Variant = variant,
				Description = row.Instrument.ToString(),
				Category = PerformanceCategory,
				EquipableClasses = -1,
				EquipRaceMask = 0,
				UiCategory = "Performance",
			};

			yield return (item, ["mainHand"]);
		}
	}

	private static CatalogItemDto CreateGear(
		uint itemId,
		string name,
		string description,
		ushort modelBase,
		ushort variant,
		int category)
		=> new()
		{
			ItemId = itemId,
			Name = name,
			Description = description,
			Set = 0,
			Base = modelBase,
			Variant = variant,
			Category = category,
			EquipableClasses = -1,
			EquipRaceMask = 0,
			UiCategory = "Special",
		};
}
