// © DeadlyDeathAngel.
// Licensed under the MIT license.

namespace LuminusBridge.Services;

using LuminusBridge.Api;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;

/// <summary>
/// Reads and sets player targets via Dalamud ITargetManager.
/// </summary>
public sealed class BridgeTargetService
{
	private readonly IClientState clientState;
	private readonly IObjectTable objectTable;
	private readonly ITargetManager targetManager;

	public BridgeTargetService(IClientState clientState, IObjectTable objectTable, ITargetManager targetManager)
	{
		this.clientState = clientState;
		this.objectTable = objectTable;
		this.targetManager = targetManager;
	}

	public BridgeActorDto? GetLocalPlayer()
		=> this.objectTable.LocalPlayer is { } local && local.IsValid()
			? ToDto(local, isLocalPlayer: true)
			: null;

	public BridgeActorDto? GetCurrentTarget()
	{
		IGameObject? target = this.clientState.IsGPosing
			? this.targetManager.GPoseTarget ?? this.targetManager.Target
			: this.targetManager.Target;

		return target != null && target.IsValid() ? ToDto(target) : null;
	}

	public bool TrySetTarget(int objectIndex, out string? error)
	{
		error = null;
		if (objectIndex < 0 || objectIndex >= this.objectTable.Length)
		{
			error = $"Invalid object index {objectIndex}.";
			return false;
		}

		IGameObject? target = this.objectTable[objectIndex];
		if (target == null || !target.IsValid())
		{
			error = $"No valid actor at index {objectIndex}.";
			return false;
		}

		if (this.clientState.IsGPosing)
		{
			this.targetManager.GPoseTarget = target;
		}
		else
		{
			this.targetManager.Target = target;
		}

		return true;
	}

	private BridgeActorDto ToDto(IGameObject obj, bool isLocalPlayer = false)
	{
		bool local = isLocalPlayer
			|| (this.objectTable.LocalPlayer?.ObjectIndex == obj.ObjectIndex);

		ActorWorldInfo.TryGetHomeWorld(obj, out uint homeWorldId, out string homeWorld);

		return new BridgeActorDto
		{
			ObjectIndex = obj.ObjectIndex,
			Address = $"0x{obj.Address:X}",
			Name = obj.Name.TextValue,
			DataId = obj.BaseId,
			ObjectKind = (byte)obj.ObjectKind,
			Distance = 0,
			IsLocalPlayer = local,
			IsGposeActor = obj.ObjectIndex is >= 200 and < 440,
			HomeWorldId = homeWorldId,
			HomeWorld = homeWorld,
		};
	}
}
