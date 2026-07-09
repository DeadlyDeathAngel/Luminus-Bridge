// © DeadlyDeathAngel.
// Licensed under the MIT license.

namespace LuminusBridge.Services;

using LuminusBridge.Api;
using Dalamud;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using System;
using System.Collections.Generic;

/// <summary>
/// GPose actor animation override + speed control (desktop <see cref="Luminus.Services.AnimationService"/>).
/// </summary>
public sealed unsafe class ActorAnimationService : IDisposable
{
	private const string AnimationSpeedPatchSignature =
		"F3 0F 11 94 ?? ?? ?? ?? ?? 80 89 ?? ?? ?? ?? 01";

	private const int AnimationSpeedPatchNopCount = 9;
	private const int AnimationBlockOffset = 0x0A30;
	private const int AnimationIdsOffset = 0x0F0;
	private const int AnimationSpeedsOffset = 0x164;
	private const int AnimationSpeedTriggerOffset = 0x1F2;
	private const int AnimationBaseOverrideOffset = 0x2E6;
	private const int AnimationLipsOverrideOffset = 0x2E8;
	private const int CharacterModeOffset = 0x2364;
	private const int CharacterModeInputOffset = 0x2365;
	private const int AnimationSlotCount = 14;
	private const int FullBodySlot = 0;

	private const ushort IdleAnimationId = 3;
	private const ushort DrawWeaponAnimationId = 34;

	private const byte CharacterModeNormal = 1;
	private const byte CharacterModeAnimLock = 8;

	private readonly IObjectTable objectTable;
	private readonly GameStateService gameState;
	private readonly ISigScanner sigScanner;
	private readonly Dictionary<int, bool> linkSpeedsByActor = [];

	private nint speedPatchAddress;
	private byte[]? speedPatchOriginal;
	private bool speedPatchResolveAttempted;
	private bool speedPatchActive;
	private bool speedControlEnabled;

	public ActorAnimationService(
		IObjectTable objectTable,
		GameStateService gameState,
		ISigScanner sigScanner)
	{
		this.objectTable = objectTable;
		this.gameState = gameState;
		this.sigScanner = sigScanner;
	}

	public bool SpeedControlEnabled => this.speedControlEnabled;

	public void Dispose()
	{
		this.SetSpeedPatchActive(false);
	}

	public AnimationResponse TryGetAnimation(int objectIndex)
	{
		if (!this.TryResolveCharacter(objectIndex, out BattleChara* battleChara, out string? error))
		{
			return Fail(objectIndex, error ?? "Actor not found.");
		}

		return this.BuildResponse(objectIndex, battleChara, ok: true);
	}

	public AnimationResponse TrySetAnimation(int objectIndex, AnimationUpdateRequest request)
	{
		GameStateSnapshot state = this.gameState.Current;
		if (!state.IsInGpose)
		{
			return Fail(objectIndex, "Animation can only be changed in GPose.");
		}

		if (!this.TryResolveCharacter(objectIndex, out BattleChara* battleChara, out string? error))
		{
			return Fail(objectIndex, error ?? "Actor not found.");
		}

		if (!this.CanAnimate(battleChara) && request.Reset != true)
		{
			return Fail(objectIndex, "Actor cannot be animated in the current character mode.");
		}

		if (request.Reset == true)
		{
			this.ResetAnimation(battleChara, objectIndex);
		}
		else if (!string.IsNullOrWhiteSpace(request.Preset))
		{
			ushort? presetId = ResolvePreset(request.Preset);
			if (presetId == null)
			{
				return Fail(objectIndex, $"Unknown animation preset '{request.Preset}'.");
			}

			this.ApplyOverride(battleChara, presetId.Value, interrupt: true);
		}
		else if (request.BaseAnimationId.HasValue)
		{
			this.ApplyOverride(battleChara, request.BaseAnimationId.Value, request.Interrupt ?? true);
		}

		if (request.LinkSpeeds.HasValue)
		{
			this.linkSpeedsByActor[objectIndex] = request.LinkSpeeds.Value;
		}

		if (request.Speed.HasValue)
		{
			bool linkSpeeds = this.linkSpeedsByActor.TryGetValue(objectIndex, out bool linked) ? linked : true;
			this.SetSpeed(battleChara, request.Speed.Value, linkSpeeds);
		}

		return this.BuildResponse(objectIndex, battleChara, ok: true);
	}

	private AnimationResponse BuildResponse(int objectIndex, BattleChara* battleChara, bool ok)
	{
		nint actor = (nint)battleChara;
		nint animation = actor + AnimationBlockOffset;
		byte characterMode = *(byte*)(actor + CharacterModeOffset);
		bool linkSpeeds = this.linkSpeedsByActor.TryGetValue(objectIndex, out bool linked) ? linked : true;

		return new AnimationResponse
		{
			Ok = ok,
			ObjectIndex = objectIndex,
			BaseAnimationId = *(ushort*)(animation + AnimationBaseOverrideOffset),
			FullBodyAnimationId = *(ushort*)(animation + AnimationIdsOffset + (FullBodySlot * sizeof(ushort))),
			Speed = *(float*)(animation + AnimationSpeedsOffset + (FullBodySlot * sizeof(float))),
			LinkSpeeds = linkSpeeds,
			IsOverridden = characterMode == CharacterModeAnimLock,
			CharacterMode = characterMode,
			SpeedControlEnabled = this.speedControlEnabled,
			IsInGpose = this.gameState.Current.IsInGpose,
		};
	}

	private void ApplyOverride(BattleChara* battleChara, ushort animationId, bool interrupt)
	{
		nint actor = (nint)battleChara;
		nint animation = actor + AnimationBlockOffset;

		*(ushort*)(animation + AnimationBaseOverrideOffset) = animationId;
		*(byte*)(actor + CharacterModeInputOffset) = 0;
		*(byte*)(actor + CharacterModeOffset) = CharacterModeAnimLock;

		if (interrupt)
		{
			*(ushort*)(animation + AnimationIdsOffset + (FullBodySlot * sizeof(ushort))) = 0;
		}
	}

	private void ResetAnimation(BattleChara* battleChara, int objectIndex)
	{
		nint actor = (nint)battleChara;
		nint animation = actor + AnimationBlockOffset;

		*(ushort*)(animation + AnimationBaseOverrideOffset) = 0;
		*(ushort*)(animation + AnimationLipsOverrideOffset) = 0;
		*(byte*)(actor + CharacterModeInputOffset) = 0;
		*(byte*)(actor + CharacterModeOffset) = CharacterModeNormal;
		this.linkSpeedsByActor[objectIndex] = true;
		this.SetSpeed(battleChara, 1.0f, linkSpeeds: true);
	}

	private void SetSpeed(BattleChara* battleChara, float speed, bool linkSpeeds)
	{
		this.SetSpeedControlEnabled(true);

		nint animation = (nint)battleChara + AnimationBlockOffset;
		nint speeds = animation + AnimationSpeedsOffset;

		*(float*)(speeds + (FullBodySlot * sizeof(float))) = speed;
		if (linkSpeeds)
		{
			for (int i = 1; i < AnimationSlotCount; i++)
			{
				*(float*)(speeds + (i * sizeof(float))) = speed;
			}
		}

		*(byte*)(animation + AnimationSpeedTriggerOffset) = 1;
	}

	private void SetSpeedControlEnabled(bool enabled)
	{
		if (this.speedControlEnabled == enabled)
		{
			return;
		}

		this.speedControlEnabled = enabled;
		this.SetSpeedPatchActive(enabled);
	}

	private void SetSpeedPatchActive(bool active)
	{
		if (active)
		{
			this.EnsureSpeedPatch();
		}

		if (this.speedPatchAddress == 0 || this.speedPatchOriginal == null)
		{
			return;
		}

		if (active == this.speedPatchActive)
		{
			return;
		}

		if (active)
		{
			byte[] nops = new byte[AnimationSpeedPatchNopCount];
			Array.Fill(nops, (byte)0x90);
			SafeMemory.WriteBytes(this.speedPatchAddress, nops);
		}
		else
		{
			SafeMemory.WriteBytes(this.speedPatchAddress, this.speedPatchOriginal);
		}

		this.speedPatchActive = active;
	}

	private void EnsureSpeedPatch()
	{
		if (this.speedPatchOriginal != null || this.speedPatchResolveAttempted)
		{
			return;
		}

		this.speedPatchResolveAttempted = true;
		if (!this.sigScanner.TryScanText(AnimationSpeedPatchSignature, out nint address) || address == 0)
		{
			return;
		}

		if (!SafeMemory.ReadBytes(address, AnimationSpeedPatchNopCount, out byte[]? original) || original == null)
		{
			return;
		}

		this.speedPatchAddress = address;
		this.speedPatchOriginal = original;
	}

	private static ushort? ResolvePreset(string preset)
		=> preset.Trim().ToLowerInvariant() switch
		{
			"idle" => IdleAnimationId,
			"drawweapon" or "draw_weapon" or "draw-weapon" => DrawWeaponAnimationId,
			_ => null,
		};

	private bool CanAnimate(BattleChara* battleChara)
	{
		byte mode = *(byte*)((nint)battleChara + CharacterModeOffset);
		return mode is CharacterModeNormal or CharacterModeAnimLock;
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

	private static AnimationResponse Fail(int objectIndex, string error)
		=> new()
		{
			Ok = false,
			ObjectIndex = objectIndex,
			Error = error,
		};
}
