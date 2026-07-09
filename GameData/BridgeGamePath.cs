namespace LuminusBridge.GameData;

using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using System;
using System.IO;

internal static class BridgeGamePath
{
	public static string? FindGameDirectory(IDalamudPluginInterface pluginInterface)
	{
		string? start = pluginInterface.AssemblyLocation.Directory?.FullName;
		if (!string.IsNullOrEmpty(start))
		{
			string? fromPlugin = WalkForGameFolder(start);
			if (fromPlugin != null)
			{
				return fromPlugin;
			}
		}

		string? processPath = Environment.ProcessPath;
		if (!string.IsNullOrEmpty(processPath))
		{
			string? fromProcess = WalkForGameFolder(Path.GetDirectoryName(processPath) ?? string.Empty);
			if (fromProcess != null)
			{
				return fromProcess;
			}
		}

		string? env = Environment.GetEnvironmentVariable("FFXIV_GAME_PATH");
		if (!string.IsNullOrWhiteSpace(env) && Directory.Exists(env))
		{
			return env;
		}

		return null;
	}

	private static string? WalkForGameFolder(string start)
	{
		DirectoryInfo? dir = new DirectoryInfo(start);
		for (int i = 0; i < 10 && dir != null; i++)
		{
			string gameDir = Path.Combine(dir.FullName, "game");
			if (File.Exists(Path.Combine(gameDir, "ffxivgame.ver"))
				|| File.Exists(Path.Combine(gameDir, "XivMods.json")))
			{
				return gameDir;
			}

			if (File.Exists(Path.Combine(dir.FullName, "ffxivgame.ver")))
			{
				return dir.FullName;
			}

			dir = dir.Parent;
		}

		return null;
	}
}
