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
	private readonly ActorAppearanceService appearanceService;

	public ActorRedrawService(ActorAppearanceService appearanceService)
	{
		this.appearanceService = appearanceService;
	}

	public bool TryRedraw(int objectIndex, out string? error)
		=> this.appearanceService.TryRefreshModel(objectIndex, out error);
}
