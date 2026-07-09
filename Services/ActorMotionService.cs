// © DeadlyDeathAngel.
// Licensed under the MIT license.

namespace LuminusBridge.Services;

using LuminusBridge.Api;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using System;

/// <summary>
/// Reads/writes GPose actor motion disable (Luminus <c>ActorMemory.IsMotionDisabled</c> @ 0x1BA4).
/// </summary>
public sealed unsafe class ActorMotionService
{
	// Matches Luminus.ActorMemory.IsMotionDisabled bind offset.
	private const int IsMotionDisabledOffset = 0x1BA4;

	private readonly IObjectTable objectTable;
	private readonly GameStateService gameState;

	public ActorMotionService(IObjectTable objectTable, GameStateService gameState)
	{
		this.objectTable = objectTable;
		this.gameState = gameState;
	}

	public MotionResponse TryGetMotion(int objectIndex)
	{
		if (!this.TryResolveCharacter(objectIndex, out BattleChara* battleChara, out string? error))
		{
			return Fail(objectIndex, error ?? "Actor not found.");
		}

		bool motionDisabled = ReadMotionDisabled(battleChara);
		return new MotionResponse
		{
			Ok = true,
			ObjectIndex = objectIndex,
			MotionDisabled = motionDisabled,
			IsMotionEnabled = !motionDisabled,
			IsInGpose = this.gameState.Current.IsInGpose,
		};
	}

	public MotionResponse TrySetMotion(int objectIndex, MotionUpdateRequest request)
	{
		if (!request.MotionDisabled.HasValue)
		{
			return Fail(objectIndex, "motionDisabled is required.");
		}

		GameStateSnapshot state = this.gameState.Current;
		if (!state.IsInGpose)
		{
			return Fail(objectIndex, "Actor motion can only be changed in GPose.");
		}

		if (!this.TryResolveCharacter(objectIndex, out BattleChara* battleChara, out string? error))
		{
			return Fail(objectIndex, error ?? "Actor not found.");
		}

		WriteMotionDisabled(battleChara, request.MotionDisabled.Value);
		bool motionDisabled = ReadMotionDisabled(battleChara);
		return new MotionResponse
		{
			Ok = true,
			ObjectIndex = objectIndex,
			MotionDisabled = motionDisabled,
			IsMotionEnabled = !motionDisabled,
			IsInGpose = state.IsInGpose,
		};
	}

	private bool TryResolveCharacter(int objectIndex, out BattleChara* battleChara, out string? error)
	{
		battleChara = null;
		error = null;

		if (objectIndex < 0 || objectIndex >= this.objectTable.Length)
		{
			error = $"Invalid object index {objectIndex}.";
			return false;
		}

		if (this.objectTable[objectIndex] is not ICharacter character || !character.IsValid())
		{
			error = $"No character at index {objectIndex}.";
			return false;
		}

		battleChara = (BattleChara*)character.Address;
		if (battleChara == null)
		{
			error = "Character address unavailable.";
			return false;
		}

		return true;
	}

	private static bool ReadMotionDisabled(BattleChara* battleChara)
		=> *(byte*)((nint)battleChara + IsMotionDisabledOffset) != 0;

	private static void WriteMotionDisabled(BattleChara* battleChara, bool disabled)
		=> *(byte*)((nint)battleChara + IsMotionDisabledOffset) = disabled ? (byte)1 : (byte)0;

	private static MotionResponse Fail(int objectIndex, string error)
		=> new()
		{
			Ok = false,
			ObjectIndex = objectIndex,
			Error = error,
		};
}
