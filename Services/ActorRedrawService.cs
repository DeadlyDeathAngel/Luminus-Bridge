// © DeadlyDeathAngel.
// Licensed under the MIT license.

namespace AnamnesisBridge.Services;

using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using System;

/// <summary>
/// Refreshes the on-screen model from current customize data, then re-applies
/// frame overrides (height/skin). Does not restore character-creation defaults.
/// </summary>
public sealed class ActorRedrawService
{
	private const int GposeActorIndexMin = 200;
	private const int GposeActorIndexMax = 439;

	private readonly ActorAppearanceService appearanceService;

	public ActorRedrawService(ActorAppearanceService appearanceService)
	{
		this.appearanceService = appearanceService;
	}

	public bool TryRedraw(int objectIndex, out string? error)
		=> this.appearanceService.TryRefreshModel(objectIndex, out error);

	/// <summary>Redraw local player and GPose slot actors (hair/physics reset after posing).</summary>
	public int TryRedrawPosingActors(IObjectTable objectTable)
	{
		int redrawn = 0;
		if (objectTable.LocalPlayer is { } localPlayer && this.TryRedrawIfCharacter(localPlayer.ObjectIndex, objectTable))
		{
			redrawn++;
		}

		int length = objectTable.Length;
		for (int objectIndex = GposeActorIndexMin; objectIndex <= GposeActorIndexMax && objectIndex < length; objectIndex++)
		{
			if (this.TryRedrawIfCharacter(objectIndex, objectTable))
			{
				redrawn++;
			}
		}

		return redrawn;
	}

	private bool TryRedrawIfCharacter(int objectIndex, IObjectTable objectTable)
	{
		if (objectIndex < 0 || objectIndex >= objectTable.Length)
		{
			return false;
		}

		if (objectTable[objectIndex] is not ICharacter character || !character.IsValid())
		{
			return false;
		}

		return this.TryRedraw(objectIndex, out _);
	}
}
