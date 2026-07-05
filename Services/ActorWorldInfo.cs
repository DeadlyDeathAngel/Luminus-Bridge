// © DeadlyDeathAngel.
// Licensed under the MIT license.

namespace AnamnesisBridge.Services;

using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;

/// <summary>
/// Home-world helpers for player characters (names alone are not unique across worlds).
/// </summary>
internal static class ActorWorldInfo
{
	public static void TryGetHomeWorld(IGameObject obj, out uint homeWorldId, out string homeWorldName)
	{
		homeWorldId = 0;
		homeWorldName = string.Empty;

		if (obj is not IPlayerCharacter player)
		{
			return;
		}

		try
		{
			homeWorldId = player.HomeWorld.RowId;
			homeWorldName = player.HomeWorld.ValueNullable?.Name.ToString() ?? string.Empty;
		}
		catch
		{
			// Sheet lookup can fail during zoning.
		}
	}
}
