// © DeadlyDeathAngel.
// Licensed under the MIT license.

namespace LuminusBridge.GameData;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

internal static class BridgeItemCategoryIndex
{
	private const int Standard = 1 << 0;
	private const int Premium = 1 << 1;
	private const int Limited = 1 << 2;
	private const int Deprecated = 1 << 3;
	private const int CustomEquipment = 1 << 4;
	private const int Performance = 1 << 5;

	private static Dictionary<uint, int>? categories;

	public static int GetCategory(uint itemId)
	{
		Ensure();
		return categories!.TryGetValue(itemId, out int value) ? value : 0;
	}

	private static void Ensure()
	{
		if (categories != null)
		{
			return;
		}

		categories = new Dictionary<uint, int>();
		Assembly assembly = typeof(BridgeItemCategoryIndex).Assembly;
		string resourceName = assembly.GetManifestResourceNames()
			.FirstOrDefault(n => n.EndsWith("ItemCategories.json", StringComparison.OrdinalIgnoreCase))
			?? throw new InvalidOperationException("ItemCategories.json embedded resource missing.");

		using Stream? stream = assembly.GetManifestResourceStream(resourceName);
		if (stream == null)
		{
			return;
		}

		using var document = JsonDocument.Parse(stream, BridgeEmbeddedJson.DocumentOptions);
		foreach (JsonProperty property in document.RootElement.EnumerateObject())
		{
			if (!uint.TryParse(property.Name, out uint itemId))
			{
				continue;
			}

			string? raw = property.Value.GetString();
			if (string.IsNullOrWhiteSpace(raw))
			{
				continue;
			}

			categories[itemId] = ParseFlags(raw);
		}
	}

	private static int ParseFlags(string raw)
	{
		int flags = 0;
		foreach (string part in raw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
		{
			flags |= part switch
			{
				"Standard" => Standard,
				"Premium" => Premium,
				"Limited" => Limited,
				"Deprecated" => Deprecated,
				"CustomEquipment" => CustomEquipment,
				"Performance" => Performance,
				_ => 0,
			};
		}

		return flags;
	}
}
