// © DeadlyDeathAngel.
// Licensed under the MIT license.

namespace AnamnesisBridge.Services;

using AnamnesisBridge.Api;

/// <summary>
/// In-process command IPC for the Linux host (replaces injected RemoteController shared-memory IPC).
/// Tracks pose/freeze flags and applies what Dalamud can do safely without ANAMCTRL.
/// </summary>
public sealed class BridgeIpcService
{
	private readonly GameStateService gameState;
	private readonly BridgePosingHooks posingHooks;
	private readonly object gate = new();

	private bool posingEnabled;
	private bool freezePhysics;
	private bool freezeWorldVisualState;

	public BridgeIpcService(GameStateService gameState, BridgePosingHooks posingHooks)
	{
		this.gameState = gameState;
		this.posingHooks = posingHooks;
	}

	public BridgeIpcStatusDto Snapshot()
	{
		GameStateSnapshot state = this.gameState.Current;
		lock (this.gate)
		{
			// Auto-disable posing and world freeze when leaving GPose.
			if (!state.IsInGpose && (this.posingEnabled || this.freezeWorldVisualState))
			{
				this.posingEnabled = false;
				this.freezePhysics = false;
				this.freezeWorldVisualState = false;
				this.posingHooks.DisableAll();
			}

			return new BridgeIpcStatusDto
			{
				Ok = true,
				Transport = "anamnesis-bridge-http",
				SignedIn = state.SignedIn,
				IsInGpose = state.IsInGpose,
				PosingEnabled = this.posingEnabled,
				FreezePhysics = this.freezePhysics,
				FreezeWorldVisualState = this.freezeWorldVisualState,
				Capabilities =
				[
					"GetPosingEnabled",
					"SetPosingEnabled",
					"GetFreezePhysics",
					"SetFreezePhysics",
					"GetFreezeWorldVisualState",
					"SetFreezeWorldVisualState",
					"GetIsInGpose",
				],
				PosingHooksActive = this.posingHooks.HooksActive,
			};
		}
	}

	public BridgeIpcCommandResponse Execute(BridgeIpcCommandRequest request)
	{
		if (string.IsNullOrWhiteSpace(request.Command))
		{
			return Fail("Missing command.");
		}

		string command = request.Command.Trim();
		GameStateSnapshot state = this.gameState.Current;

		lock (this.gate)
		{
			switch (command)
			{
				case "GetIsInGpose":
					return Ok(state.IsInGpose);

				case "GetPosingEnabled":
					return Ok(this.posingEnabled && state.IsInGpose);

				case "SetPosingEnabled":
					if (!request.Value.HasValue)
					{
						return Fail("SetPosingEnabled requires value.");
					}

					if (request.Value.Value && !state.IsInGpose)
					{
						return Fail("Cannot enable posing outside GPose.");
					}

					this.posingEnabled = request.Value.Value;
					if (this.posingEnabled)
					{
						this.freezePhysics = true;
					}

					this.SyncPosingHooks();
					return Ok(this.posingEnabled);

				case "GetFreezePhysics":
					return Ok(this.freezePhysics);

				case "SetFreezePhysics":
					if (!request.Value.HasValue)
					{
						return Fail("SetFreezePhysics requires value.");
					}

					this.freezePhysics = request.Value.Value;
					this.SyncPosingHooks();
					return Ok(this.freezePhysics);

				case "GetFreezeWorldVisualState":
					return Ok(this.freezeWorldVisualState);

				case "SetFreezeWorldVisualState":
					if (!request.Value.HasValue)
					{
						return Fail("SetFreezeWorldVisualState requires value.");
					}

					if (request.Value.Value && !state.IsInGpose)
					{
						return Fail("Cannot enable world freeze outside GPose.");
					}

					this.freezeWorldVisualState = request.Value.Value;
					this.SyncPosingHooks();
					return Ok(this.freezeWorldVisualState);

				default:
					return Fail($"Unknown command '{command}'.");
			}
		}
	}

	private void SyncPosingHooks()
		=> this.posingHooks.ApplyState(this.posingEnabled, this.freezePhysics, this.freezeWorldVisualState);

	private static BridgeIpcCommandResponse Ok(bool value)
		=> new() { Ok = true, Value = value };

	private static BridgeIpcCommandResponse Fail(string error)
		=> new() { Ok = false, Error = error };
}
