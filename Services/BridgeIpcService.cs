// © DeadlyDeathAngel.
// Licensed under the MIT license.

namespace AnamnesisBridge.Services;

using AnamnesisBridge.Api;
using System;

/// <summary>
/// In-process command IPC for the Linux host (replaces injected RemoteController shared-memory IPC).
/// Tracks pose/freeze flags and applies what Dalamud can do safely without ANAMCTRL.
/// </summary>
public sealed class BridgeIpcService
{
	private readonly GameStateService gameState;
	private readonly BridgePosingHooks posingHooks;
	private readonly BridgeGposeControllerService gposeController;
	private readonly object gate = new();

	private bool posingEnabled;
	private bool freezePhysics;
	private bool freezeWorldVisualState;
	private bool lastKnownGpose;
	private bool posingUsedThisGposeSession;
	private int cameraSettleTicksRemaining;
	private Func<bool>? getPosingHooksEngaged;

	private const int CameraSettleTicks = 18;

	public void SetPosingHooksEngagedProvider(Func<bool> provider)
		=> this.getPosingHooksEngaged = provider;

	public BridgeIpcService(
		GameStateService gameState,
		BridgePosingHooks posingHooks,
		BridgeGposeControllerService gposeController)
	{
		this.gameState = gameState;
		this.posingHooks = posingHooks;
		this.gposeController = gposeController;
	}

	public void InitializeGposeState(bool isInGpose)
	{
		lock (this.gate)
		{
			this.lastKnownGpose = isInGpose;
		}
	}

	public bool ArePosingHooksAllowed()
	{
		lock (this.gate)
		{
			return this.cameraSettleTicksRemaining <= 0;
		}
	}

	/// <summary>Framework tick — camera settle countdown and Face/Gaze guard while posing.</summary>
	public void OnFrameworkTick(bool isInGpose)
	{
		lock (this.gate)
		{
			if (!isInGpose)
			{
				this.cameraSettleTicksRemaining = 0;
				return;
			}

			if (this.cameraSettleTicksRemaining > 0)
			{
				this.cameraSettleTicksRemaining--;
			}

			if (!this.posingEnabled)
			{
				return;
			}

			GposeCameraState cameras = this.gposeController.Snapshot();
			if (cameras.FaceCameraEnabled || cameras.GazeCameraEnabled)
			{
				PrepareForPosingResult result = this.gposeController.PrepareForPosing();
				if (result.DisabledFaceCamera || result.DisabledGazeCamera)
				{
					this.cameraSettleTicksRemaining = CameraSettleTicks;
				}
			}
		}
	}

	public BridgeIpcStatusDto Snapshot()
	{
		GameStateSnapshot state = this.gameState.Current;
		lock (this.gate)
		{
			// Auto-disable posing and world freeze when leaving GPose.
			if (!state.IsInGpose && (this.posingEnabled || this.freezeWorldVisualState))
			{
				this.ResetSessionStateLocked();
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
				PosingHooksEngaged = this.posingEnabled
					&& state.IsInGpose
					&& (this.getPosingHooksEngaged?.Invoke() ?? false),
			};
		}
	}

	/// <summary>Clears posing/freeze flags and hook state (logout, login, relog).</summary>
	public void ResetSessionState()
	{
		lock (this.gate)
		{
			this.ResetSessionStateLocked();
		}
	}

	/// <summary>Framework-thread GPose transition (independent of HTTP client polling).</summary>
	public bool UpdateGposeState(bool isInGpose, out bool enteredGpose)
	{
		enteredGpose = false;
		lock (this.gate)
		{
			bool wasInGpose = this.lastKnownGpose;
			this.lastKnownGpose = isInGpose;

			if (!wasInGpose && isInGpose)
			{
				this.posingUsedThisGposeSession = false;
				enteredGpose = true;
			}

			if (wasInGpose && !isInGpose)
			{
				bool needsRestore = this.posingUsedThisGposeSession
					|| this.posingEnabled
					|| this.freezePhysics
					|| this.freezeWorldVisualState;
				this.ResetSessionStateLocked();
				this.posingUsedThisGposeSession = false;
				return needsRestore;
			}

			if (!isInGpose && (this.posingEnabled || this.freezeWorldVisualState))
			{
				this.ResetSessionStateLocked();
			}

			return false;
		}
	}

	public void MarkPosingUsed()
	{
		lock (this.gate)
		{
			this.posingUsedThisGposeSession = true;
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

					if (request.Value.Value)
					{
						PrepareForPosingResult prepare = this.gposeController.PrepareForPosing();
						if (prepare.DisabledFaceCamera || prepare.DisabledGazeCamera)
						{
							this.cameraSettleTicksRemaining = CameraSettleTicks;
						}
					}
					else
					{
						this.cameraSettleTicksRemaining = 0;
					}

					this.posingEnabled = request.Value.Value;
					if (this.posingEnabled)
					{
						this.freezePhysics = true;
						this.posingUsedThisGposeSession = true;
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

	private void ResetSessionStateLocked()
	{
		this.posingEnabled = false;
		this.freezePhysics = false;
		this.freezeWorldVisualState = false;
		this.cameraSettleTicksRemaining = 0;
		this.posingHooks.DisableAll();
		this.SyncPosingHooks();
	}

	private void SyncPosingHooks()
		=> this.posingHooks.ApplyState(this.posingEnabled, this.freezePhysics, this.freezeWorldVisualState);

	private static BridgeIpcCommandResponse Ok(bool value)
		=> new() { Ok = true, Value = value };

	private static BridgeIpcCommandResponse Fail(string error)
		=> new() { Ok = false, Error = error };
}
