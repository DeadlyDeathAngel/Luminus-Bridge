// © Luminus.
// Licensed under the MIT license.

namespace LuminusBridge.Services;

using Dalamud.Plugin.Services;

/// <summary>
/// Caches game state on the framework thread for HTTP handlers.
/// </summary>
public sealed class GameStateService
{
	private readonly IClientState clientState;
	private GameStateSnapshot snapshot = new();

	public GameStateService(IClientState clientState)
	{
		this.clientState = clientState;
	}

	public GameStateSnapshot Current => this.snapshot;

	public void Update()
	{
		this.snapshot = new GameStateSnapshot
		{
			FrameworkTick = true,
			IsLoggedIn = this.clientState.IsLoggedIn,
			IsInGpose = this.clientState.IsGPosing,
			TerritoryId = this.clientState.TerritoryType,
		};
	}
}

public readonly struct GameStateSnapshot
{
	public bool FrameworkTick { get; init; }

	public bool IsLoggedIn { get; init; }

	public bool IsInGpose { get; init; }

	public uint TerritoryId { get; init; }

	public bool SignedIn => this.IsLoggedIn && this.TerritoryId > 0;
}
