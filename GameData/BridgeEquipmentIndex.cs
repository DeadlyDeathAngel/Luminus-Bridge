// © DeadlyDeathAngel.
// Licensed under the MIT license.

namespace AnamnesisBridge.GameData;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using AnamnesisBridge.Api;

internal sealed class EquipmentJsonRow
{
	[JsonPropertyName("Name")]
	public string Name { get; set; } = string.Empty;

	[JsonPropertyName("Id")]
	public string Id { get; set; } = string.Empty;

	[JsonPropertyName("Slot")]
	public string Slot { get; set; } = string.Empty;

	[JsonPropertyName("Description")]
	public string? Description { get; set; }
}

internal static class BridgeEquipmentIndex
{
	private const int CustomEquipmentCategory = 1 << 4;
	public const uint CustomItemIdBase = 0x9000_0000;

	public static IEnumerable<(CatalogItemDto Item, IReadOnlyList<string> Slots)> LoadEntries()
	{
		Assembly assembly = typeof(BridgeEquipmentIndex).Assembly;
		string resourceName = assembly.GetManifestResourceNames()
			.FirstOrDefault(n => n.EndsWith("Equipment.json", StringComparison.OrdinalIgnoreCase))
			?? throw new InvalidOperationException("Equipment.json embedded resource missing.");

		using Stream? stream = assembly.GetManifestResourceStream(resourceName);
		if (stream == null)
		{
			yield break;
		}

		List<EquipmentJsonRow>? rows = JsonSerializer.Deserialize<List<EquipmentJsonRow>>(stream);
		if (rows == null)
		{
			yield break;
		}

		for (int i = 0; i < rows.Count; i++)
		{
			EquipmentJsonRow row = rows[i];
			if (string.IsNullOrWhiteSpace(row.Name) || string.IsNullOrWhiteSpace(row.Id))
			{
				continue;
			}

			(ushort set, ushort modelBase, ushort variant) = ParseModel(row.Id);
			if (modelBase == 0 && set == 0)
			{
				continue;
			}

			var item = new CatalogItemDto
			{
				ItemId = CustomItemIdBase + (uint)i,
				Name = row.Name,
				Set = set,
				Base = modelBase,
				Variant = variant,
				Description = row.Description ?? string.Empty,
				Category = CustomEquipmentCategory,
				EquipableClasses = -1,
				EquipRaceMask = 0,
				UiCategory = "Custom",
			};

			yield return (item, SlotsForEquipmentJson(row.Slot));
		}
	}

	private static (ushort Set, ushort Base, ushort Variant) ParseModel(string id)
	{
		string[] parts = id.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		if (parts.Length == 3)
		{
			return (ushort.Parse(parts[0]), ushort.Parse(parts[1]), ushort.Parse(parts[2]));
		}

		if (parts.Length == 2)
		{
			return (0, ushort.Parse(parts[0]), ushort.Parse(parts[1]));
		}

		return (0, 0, 0);
	}

	private static IReadOnlyList<string> SlotsForEquipmentJson(string slot)
		=> slot switch
		{
			"Weapons" => ["mainHand"],
			"OffHand" => ["offHand"],
			"Head" => ["head"],
			"Armor" => ["head", "body", "hands", "legs", "feet"],
			_ => [],
		};
}
