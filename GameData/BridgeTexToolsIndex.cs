// © DeadlyDeathAngel.
// Licensed under the MIT license.

namespace AnamnesisBridge.GameData;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dalamud.Plugin;

internal sealed class TexToolsModEntry
{
	[JsonPropertyName("name")]
	public string Name { get; set; } = string.Empty;

	[JsonPropertyName("enabled")]
	public bool Enabled { get; set; }

	[JsonPropertyName("modPack")]
	public TexToolsModPackEntry? ModPack { get; set; }
}

internal sealed class TexToolsModPackEntry
{
	[JsonPropertyName("name")]
	public string Name { get; set; } = string.Empty;
}

internal sealed class TexToolsModList
{
	[JsonPropertyName("mods")]
	public List<TexToolsModEntry> Mods { get; set; } = [];
}

internal static class BridgeTexToolsIndex
{
	private static readonly Dictionary<string, string> ModPackByTrimmedName = new(StringComparer.OrdinalIgnoreCase);

	public static void Initialize(IDalamudPluginInterface pluginInterface)
	{
		ModPackByTrimmedName.Clear();
		string? gameDir = BridgeGamePath.FindGameDirectory(pluginInterface);
		if (string.IsNullOrWhiteSpace(gameDir))
		{
			return;
		}

		foreach (string path in new[]
		{
			Path.Combine(gameDir, "XivMods.json"),
			Path.Combine(gameDir, "aFileThatDefinitelyDoesNotExistEverAgain.json"),
		})
		{
			if (!File.Exists(path))
			{
				continue;
			}

			try
			{
				string json = File.ReadAllText(path);
				TexToolsModList? list = JsonSerializer.Deserialize<TexToolsModList>(json);
				if (list?.Mods == null)
				{
					continue;
				}

				foreach (TexToolsModEntry mod in list.Mods)
				{
					if (!mod.Enabled || string.IsNullOrWhiteSpace(mod.Name))
					{
						continue;
					}

					string trimmed = TrimModName(mod.Name);
					string pack = mod.ModPack?.Name ?? string.Empty;
					ModPackByTrimmedName[trimmed] = pack;
				}
			}
			catch
			{
				// Ignore unreadable mod lists.
			}
		}
	}

	public static bool TryGetMod(string itemName, out string modPack)
	{
		modPack = string.Empty;
		if (ModPackByTrimmedName.Count == 0)
		{
			return false;
		}

		if (!ModPackByTrimmedName.TryGetValue(TrimModName(itemName), out modPack!))
		{
			return false;
		}

		return true;
	}

	private static string TrimModName(string name)
	{
		return name
			.Replace(" - Right", string.Empty, StringComparison.Ordinal)
			.Replace(" - Left", string.Empty, StringComparison.Ordinal)
			.Replace(" - Main Hand", string.Empty, StringComparison.Ordinal)
			.Replace(" - Off Hand", string.Empty, StringComparison.Ordinal);
	}
}
