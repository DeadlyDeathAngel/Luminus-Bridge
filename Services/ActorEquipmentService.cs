// © DeadlyDeathAngel.
// Licensed under the MIT license.

namespace AnamnesisBridge.Services;

using AnamnesisBridge.Api;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using System;
using CharacterBase = FFXIVClientStructs.FFXIV.Client.Graphics.Scene.CharacterBase;
using GameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;
using SceneHuman = FFXIVClientStructs.FFXIV.Client.Graphics.Scene.Human;
using EquipSlot = FFXIVClientStructs.FFXIV.Client.Game.Character.DrawDataContainer.EquipmentSlot;

/// <summary>
/// Equipment read/write. Apply uses plain memory writes + DisableDraw/EnableDraw only
/// (no LoadEquipment / SetEquipmentSlotModel / SetupSlotModel — those crash the client).
/// </summary>
public sealed unsafe class ActorEquipmentService
{
	private readonly IObjectTable objectTable;
	private readonly BridgeGameDataService gameData;
	private readonly IPluginLog log;

	public ActorEquipmentService(IObjectTable objectTable, BridgeGameDataService gameData, IPluginLog log)
	{
		this.objectTable = objectTable;
		this.gameData = gameData;
		this.log = log;
	}

	public ActorEquipmentDto? TryGetEquipment(int objectIndex)
	{
		try
		{
			if (!TryGetCharacter(objectIndex, out ICharacter character))
			{
				return null;
			}

			var battleChara = (BattleChara*)character.Address;
			if (battleChara == null)
			{
				return null;
			}

			// Prefer DrawData; fall back to Human when a slot is empty there (glam / sync lag).
			// Facewear/glasses are separate (Glasses0/1) — not the head equipment slot.
			EquipmentModelId head = battleChara->DrawData.Equipment(EquipSlot.Head);
			EquipmentModelId body = battleChara->DrawData.Equipment(EquipSlot.Body);
			EquipmentModelId hands = battleChara->DrawData.Equipment(EquipSlot.Hands);
			EquipmentModelId legs = battleChara->DrawData.Equipment(EquipSlot.Legs);
			EquipmentModelId feet = battleChara->DrawData.Equipment(EquipSlot.Feet);
			EquipmentModelId ears = battleChara->DrawData.Equipment(EquipSlot.Ears);
			EquipmentModelId neck = battleChara->DrawData.Equipment(EquipSlot.Neck);
			EquipmentModelId wrists = battleChara->DrawData.Equipment(EquipSlot.Wrists);
			EquipmentModelId ringRight = battleChara->DrawData.Equipment(EquipSlot.RFinger);
			EquipmentModelId ringLeft = battleChara->DrawData.Equipment(EquipSlot.LFinger);
			ushort glasses0 = 0;
			ushort glasses1 = 0;

			if (TryGetHuman(character, out SceneHuman* human))
			{
				head = PreferEquipped(head, human->Head);
				body = PreferEquipped(body, human->Top);
				hands = PreferEquipped(hands, human->Arms);
				legs = PreferEquipped(legs, human->Legs);
				feet = PreferEquipped(feet, human->Feet);
				ears = PreferEquipped(ears, human->Ear);
				neck = PreferEquipped(neck, human->Neck);
				wrists = PreferEquipped(wrists, human->Wrist);
				ringRight = PreferEquipped(ringRight, human->RFinger);
				ringLeft = PreferEquipped(ringLeft, human->LFinger);
				glasses0 = human->Glasses0.Id;
				glasses1 = human->Glasses1.Id;
			}

			EquipmentSlotDto[] slots =
			[
				this.ToGearDto("head", head),
				this.ToGearDto("body", body),
				this.ToGearDto("hands", hands),
				this.ToGearDto("legs", legs),
				this.ToGearDto("feet", feet),
				this.ToGearDto("ears", ears),
				this.ToGearDto("neck", neck),
				this.ToGearDto("wrists", wrists),
				this.ToGearDto("ringRight", ringRight),
				this.ToGearDto("ringLeft", ringLeft),
			];

			WeaponModelId mainHand = battleChara->DrawData.Weapon(DrawDataContainer.WeaponSlot.MainHand).ModelId;
			WeaponModelId offHand = battleChara->DrawData.Weapon(DrawDataContainer.WeaponSlot.OffHand).ModelId;

			return new ActorEquipmentDto
			{
				ObjectIndex = objectIndex,
				Name = character.Name.TextValue,
				Slots = slots,
				MainHand = this.ToWeaponDto("mainHand", mainHand),
				OffHand = this.ToWeaponDto("offHand", offHand),
				Glasses0 = glasses0,
				Glasses1 = glasses1,
			};
		}
		catch
		{
			return null;
		}
	}

	public bool TryApplyEquipment(int objectIndex, ActorEquipmentDto dto, out string? error)
	{
		error = null;
		try
		{
			if (!TryGetCharacter(objectIndex, out ICharacter character))
			{
				error = $"No character at index {objectIndex}.";
				return false;
			}

			var battleChara = (BattleChara*)character.Address;
			if (battleChara == null)
			{
				error = "Invalid character address.";
				return false;
			}

			bool changed = false;

			if (dto.Slots != null)
			{
				foreach (EquipmentSlotDto slot in dto.Slots)
				{
					EquipSlot? equipSlot = ToEquipSlot(slot.Slot);
					if (equipSlot == null)
					{
						continue;
					}

					// Gear: DTO.Set = EquipmentModelId.Id (model base).
					EquipmentModelId model = default;
					model.Id = slot.Set;
					model.Variant = slot.Variant;
					model.Stain0 = slot.Dye;
					model.Stain1 = slot.Dye2;

					this.log.Information(
						"Equip apply {Slot}: id={Id} var={Variant} dye={Dye}/{Dye2}",
						slot.Slot,
						model.Id,
						model.Variant,
						model.Stain0,
						model.Stain1);

					// Write DrawData only. Human is destroyed on DisableDraw and rebuilt
					// from DrawData on EnableDraw — do not touch Human pointers here.
					battleChara->DrawData.Equipment(equipSlot.Value) = model;
					changed = true;
				}
			}

			if (dto.MainHand != null)
			{
				battleChara->DrawData.Weapon(DrawDataContainer.WeaponSlot.MainHand).ModelId =
					ToWeaponModel(dto.MainHand);
				changed = true;
			}

			if (dto.OffHand != null)
			{
				battleChara->DrawData.Weapon(DrawDataContainer.WeaponSlot.OffHand).ModelId =
					ToWeaponModel(dto.OffHand);
				changed = true;
			}

			if (changed)
			{
				// Same redraw path that works for race/clan changes.
				TryFullRedraw(character);
			}

			return true;
		}
		catch (Exception ex)
		{
			this.log.Error(ex, "Equipment apply failed.");
			error = ex.Message;
			return false;
		}
	}

	private bool TryGetCharacter(int objectIndex, out ICharacter character)
	{
		character = null!;
		if (objectIndex < 0 || objectIndex >= this.objectTable.Length)
		{
			return false;
		}

		if (this.objectTable[objectIndex] is not ICharacter found || !found.IsValid())
		{
			return false;
		}

		character = found;
		return true;
	}

	private static bool TryGetHuman(ICharacter character, out SceneHuman* human)
	{
		human = null;
		var battleChara = (BattleChara*)character.Address;
		if (battleChara == null || battleChara->DrawObject == null)
		{
			return false;
		}

		human = (SceneHuman*)battleChara->DrawObject;
		return human->GetModelType() == CharacterBase.ModelType.Human;
	}

	private static void TryFullRedraw(ICharacter character)
	{
		try
		{
			var gameObj = (GameObject*)character.Address;
			if (gameObj == null)
			{
				return;
			}

			ObjectKind originalKind = gameObj->ObjectKind;
			bool npcHack = originalKind == ObjectKind.Pc;
			try
			{
				if (npcHack)
				{
					gameObj->ObjectKind = ObjectKind.EventNpc;
				}

				gameObj->DisableDraw();
				gameObj->EnableDraw();
			}
			finally
			{
				if (npcHack)
				{
					gameObj->ObjectKind = originalKind;
				}
			}
		}
		catch
		{
			// Ignore redraw failures; model bytes are still written.
		}
	}

	/// <summary>Use DrawData when present; otherwise Human (drawn glam).</summary>
	private static EquipmentModelId PreferEquipped(EquipmentModelId drawData, EquipmentModelId human)
		=> drawData.Id != 0 ? drawData : human;

	private static EquipSlot? ToEquipSlot(string? slot)
		=> slot?.Trim().ToLowerInvariant() switch
		{
			"head" => EquipSlot.Head,
			"body" => EquipSlot.Body,
			"hands" => EquipSlot.Hands,
			"legs" => EquipSlot.Legs,
			"feet" => EquipSlot.Feet,
			"ears" => EquipSlot.Ears,
			"neck" => EquipSlot.Neck,
			"wrists" => EquipSlot.Wrists,
			"ringright" or "rfinger" => EquipSlot.RFinger,
			"ringleft" or "lfinger" => EquipSlot.LFinger,
			_ => null,
		};

	private EquipmentSlotDto ToGearDto(string slot, EquipmentModelId model)
	{
		var dto = new EquipmentSlotDto
		{
			Slot = slot,
			Set = model.Id,
			Variant = model.Variant,
			Dye = model.Stain0,
			Dye2 = model.Stain1,
		};
		this.gameData.ResolveGear(dto, out string itemName, out string dyeName, out string dye2Name, out string dyeHex, out string dye2Hex, out uint iconId);
		return new EquipmentSlotDto
		{
			Slot = dto.Slot,
			Set = dto.Set,
			Variant = dto.Variant,
			Dye = dto.Dye,
			Dye2 = dto.Dye2,
			ItemName = itemName,
			DyeName = dyeName,
			Dye2Name = dye2Name,
			DyeHex = dyeHex,
			Dye2Hex = dye2Hex,
			IconId = iconId,
		};
	}

	private WeaponSlotDto ToWeaponDto(string slot, WeaponModelId model)
	{
		var dto = new WeaponSlotDto
		{
			Slot = slot,
			Set = model.Id,
			Base = model.Type,
			Variant = model.Variant,
			Dye = model.Stain0,
			Dye2 = model.Stain1,
		};
		this.gameData.ResolveWeapon(dto, out string itemName, out string dyeName, out string dye2Name, out string dyeHex, out string dye2Hex, out uint iconId);
		return new WeaponSlotDto
		{
			Slot = dto.Slot,
			Set = dto.Set,
			Base = dto.Base,
			Variant = dto.Variant,
			Dye = dto.Dye,
			Dye2 = dto.Dye2,
			ItemName = itemName,
			DyeName = dyeName,
			Dye2Name = dye2Name,
			DyeHex = dyeHex,
			Dye2Hex = dye2Hex,
			IconId = iconId,
		};
	}

	private static WeaponModelId ToWeaponModel(WeaponSlotDto dto)
	{
		WeaponModelId model = default;
		model.Id = dto.Set;
		model.Type = dto.Base;
		model.Variant = dto.Variant;
		model.Stain0 = dto.Dye;
		model.Stain1 = dto.Dye2;
		return model;
	}
}
