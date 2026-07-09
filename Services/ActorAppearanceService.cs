// © DeadlyDeathAngel.
// Licensed under the MIT license.

namespace LuminusBridge.Services;

using LuminusBridge.Api;
using Dalamud.Game.ClientState.Customize;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Common.Math;
using FFXIVClientStructs.FFXIV.Shader;
using System;
using CharacterBase = FFXIVClientStructs.FFXIV.Client.Graphics.Scene.CharacterBase;
using GameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;
using SceneHuman = FFXIVClientStructs.FFXIV.Client.Graphics.Scene.Human;

/// <summary>
/// Appearance snapshot and customize writes for character actors.
/// </summary>
public sealed class ActorAppearanceService
{
	private const byte MinFace = 1;
	private const byte MaxFace = 8;
	private const int ModelScaleOffset = 0x2A4;

	private readonly IObjectTable objectTable;
	private readonly AppearanceOverrideStore overrides;

	public ActorAppearanceService(IObjectTable objectTable, AppearanceOverrideStore overrides)
	{
		this.objectTable = objectTable;
		this.overrides = overrides;
	}

	public ActorAppearanceDto? TryGetAppearance(int objectIndex)
	{
		try
		{
			if (objectIndex < 0 || objectIndex >= this.objectTable.Length)
			{
				return null;
			}

			if (this.objectTable[objectIndex] is not ICharacter character || !character.IsValid())
			{
				return null;
			}

			ICustomizeData customize = character.CustomizeData;
			Span<byte> bytes = character.Customize;
			string classJob = character.ClassJob.ValueNullable?.Name.ToString() ?? string.Empty;
			byte height = customize.Height;
			ActorWorldInfo.TryGetHomeWorld(character, out uint homeWorldId, out string homeWorld);

			unsafe
			{
				if (TryGetHuman(character, out SceneHuman* human))
				{
					float scale = *(float*)((byte*)human + ModelScaleOffset);
					if (scale is > 0.05f and < 5f)
					{
						height = (byte)Math.Clamp((int)Math.Round(scale * 50f), 0, 100);
					}
				}
			}

			return new ActorAppearanceDto
			{
				ObjectIndex = objectIndex,
				Name = character.Name.TextValue,
				HomeWorldId = homeWorldId,
				HomeWorld = homeWorld,
				Level = character.Level,
				ClassJob = classJob,
				Race = (byte)customize.Race,
				Sex = (byte)customize.Sex,
				Tribe = (byte)customize.Tribe,
				BodyType = (byte)customize.BodyType,
				Height = height,
				Face = customize.Face,
				Hairstyle = customize.Hairstyle,
				HairColor = customize.HairColor,
				HairColor2 = customize.HighlightsColor,
				SkinColor = customize.SkinColor,
				EyeColor = customize.EyeColorLeft,
				EyeColorRight = customize.EyeColorRight,
				Age = ReadByte(bytes, CustomizeOffsets.Age),
				HighlightType = ReadByte(bytes, CustomizeOffsets.HighlightType),
				FacialFeatures = ReadByte(bytes, CustomizeOffsets.FacialFeatures),
				FacialFeatureColor = ReadByte(bytes, CustomizeOffsets.FacialFeatureColor),
				Eyebrows = ReadByte(bytes, CustomizeOffsets.Eyebrows),
				Eyes = ReadByte(bytes, CustomizeOffsets.Eyes),
				Nose = ReadByte(bytes, CustomizeOffsets.Nose),
				Jaw = ReadByte(bytes, CustomizeOffsets.Jaw),
				Mouth = ReadByte(bytes, CustomizeOffsets.Mouth),
				LipsToneFurPattern = ReadByte(bytes, CustomizeOffsets.LipsToneFurPattern),
				EarMuscleTailSize = ReadByte(bytes, CustomizeOffsets.EarMuscleTailSize),
				TailEarsType = ReadByte(bytes, CustomizeOffsets.TailEarsType),
				Bust = ReadByte(bytes, CustomizeOffsets.Bust),
				FacePaint = ReadByte(bytes, CustomizeOffsets.FacePaint),
				FacePaintColor = ReadByte(bytes, CustomizeOffsets.FacePaintColor),
			};
		}
		catch
		{
			return null;
		}
	}

	public bool TryApplyAppearance(int objectIndex, ActorAppearanceDto dto, out string? error)
	{
		error = null;
		try
		{
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

			Span<byte> customize = character.Customize;
			if (customize.Length < CustomizeOffsets.Size)
			{
				error = "Customize buffer too small.";
				return false;
			}

			byte face = ClampFace(dto.Face);
			byte height = Math.Clamp(dto.Height, (byte)0, (byte)100);
			byte hairColor2 = dto.HairColor2;
			byte eyeRight = dto.EyeColorRight != 0 ? dto.EyeColorRight : dto.EyeColor;

			byte previousRace = customize[CustomizeOffsets.Race];
			byte previousTribe = customize[CustomizeOffsets.Tribe];
			byte previousGender = customize[CustomizeOffsets.Gender];

			// Identity bytes: race (1–8) + clan/tribe (1–16, e.g. Seeker/Keeper for Miqo'te).
			byte race = dto.Race is >= 1 and <= 8 ? dto.Race : previousRace;
			byte tribe = dto.Tribe is >= 1 and <= 16 ? dto.Tribe : previousTribe;
			customize[CustomizeOffsets.Race] = race;
			customize[CustomizeOffsets.Gender] = dto.Sex;
			customize[CustomizeOffsets.Tribe] = tribe;
			customize[CustomizeOffsets.Age] = dto.Age == 0 ? (byte)1 : dto.Age;

			// Hyur/Roegadyn crash if TailEarsType is left at another race's value.
			byte tailEars = dto.TailEarsType;
			if (race is 1 or 5)
			{
				tailEars = 1;
			}

			customize[CustomizeOffsets.Height] = height;
			customize[CustomizeOffsets.Head] = face;
			customize[CustomizeOffsets.Hair] = dto.Hairstyle;
			customize[CustomizeOffsets.HighlightType] = dto.HighlightType != 0
				? dto.HighlightType
				: hairColor2 != dto.HairColor ? (byte)128 : customize[CustomizeOffsets.HighlightType];
			customize[CustomizeOffsets.Skintone] = dto.SkinColor;
			customize[CustomizeOffsets.REyeColor] = eyeRight;
			customize[CustomizeOffsets.HairTone] = dto.HairColor;
			customize[CustomizeOffsets.Highlights] = hairColor2;
			customize[CustomizeOffsets.FacialFeatures] = dto.FacialFeatures;
			customize[CustomizeOffsets.FacialFeatureColor] = dto.FacialFeatureColor;
			customize[CustomizeOffsets.Eyebrows] = dto.Eyebrows;
			customize[CustomizeOffsets.LEyeColor] = dto.EyeColor;
			customize[CustomizeOffsets.Eyes] = dto.Eyes;
			customize[CustomizeOffsets.Nose] = dto.Nose;
			customize[CustomizeOffsets.Jaw] = dto.Jaw;
			customize[CustomizeOffsets.Mouth] = dto.Mouth;
			customize[CustomizeOffsets.LipsToneFurPattern] = dto.LipsToneFurPattern;
			customize[CustomizeOffsets.EarMuscleTailSize] = dto.EarMuscleTailSize;
			customize[CustomizeOffsets.TailEarsType] = tailEars;
			customize[CustomizeOffsets.Bust] = dto.Bust;
			customize[CustomizeOffsets.FacePaint] = dto.FacePaint;
			customize[CustomizeOffsets.FacePaintColor] = dto.FacePaintColor;

			TryMirrorToDrawData(character, customize);
			TryCopyHumanCustomize(character, customize);

			bool needsFullRedraw = race != previousRace
				|| tribe != previousTribe
				|| dto.Sex != previousGender;

			unsafe
			{
				if (needsFullRedraw)
				{
					TryFullRedraw(character);
				}
				else
				{
					TryUpdateDrawData(character, includeEquipment: true);
				}
			}

			this.overrides.Set(objectIndex, height, dto.SkinColor);
			TryApplyFrameOverrides(character, height, dto.SkinColor);

			if (face != dto.Face)
			{
				error = $"Face clamped to {face} (valid range {MinFace}-{MaxFace}).";
			}

			return true;
		}
		catch (Exception ex)
		{
			error = ex.Message;
			return false;
		}
	}

	/// <summary>Re-apply scale/skin overrides the game overwrites each frame.</summary>
	public void ReapplyOverrides()
	{
		foreach ((int objectIndex, AppearanceOverrideStore.AppearanceOverride overlay) in this.overrides.Snapshot())
		{
			try
			{
				if (objectIndex < 0 || objectIndex >= this.objectTable.Length)
				{
					continue;
				}

				if (this.objectTable[objectIndex] is not ICharacter character || !character.IsValid())
				{
					continue;
				}

				TryApplyFrameOverrides(character, overlay.Height, overlay.SkinTone);
			}
			catch
			{
				// Skip bad actors.
			}
		}
	}

	/// <summary>Refresh mesh from current customize, then re-hold height/skin overrides.</summary>
	public bool TryRefreshModel(int objectIndex, out string? error)
	{
		error = null;
		try
		{
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

			TryFullRedraw(character);
			this.ReapplyOverrides();
			return true;
		}
		catch (Exception ex)
		{
			error = ex.Message;
			return false;
		}
	}

	private static byte ClampFace(byte face)
		=> face < MinFace ? MinFace : face > MaxFace ? MaxFace : face;

	private static byte ReadByte(ReadOnlySpan<byte> bytes, int offset)
		=> offset >= 0 && offset < bytes.Length ? bytes[offset] : (byte)0;

	private static unsafe void TryFullRedraw(ICharacter character)
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
			// Ignore redraw failures; customize bytes are still written.
		}
	}

	private static unsafe bool TryGetHuman(ICharacter character, out SceneHuman* human)
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

	private static unsafe void TryMirrorToDrawData(ICharacter character, ReadOnlySpan<byte> customize)
	{
		try
		{
			var battleChara = (BattleChara*)character.Address;
			if (battleChara == null)
			{
				return;
			}

			CopyCosmetic(customize, battleChara->DrawData.CustomizeData.Data);
		}
		catch
		{
			// Ignore.
		}
	}

	private static unsafe void TryCopyHumanCustomize(ICharacter character, ReadOnlySpan<byte> customize)
	{
		try
		{
			if (!TryGetHuman(character, out SceneHuman* human))
			{
				return;
			}

			CopyCosmetic(customize, human->Customize.Data);
		}
		catch
		{
			// Ignore.
		}
	}

	private static unsafe void TryUpdateDrawData(ICharacter character, bool includeEquipment)
	{
		try
		{
			if (!TryGetHuman(character, out SceneHuman* human))
			{
				return;
			}

			var battleChara = (BattleChara*)character.Address;
			SceneHuman.DrawData drawData = default;

			Span<byte> source = battleChara->DrawData.CustomizeData.Data;
			Span<byte> dest = drawData.CustomizeData.Data;
			int length = Math.Min(source.Length, dest.Length);
			source[..length].CopyTo(dest[..length]);

			if (includeEquipment)
			{
				// Copy current gear so arms/feet (hand/foot skin) rebuild with new skintone.
				drawData.Equipments[0] = human->Head;
				drawData.Equipments[1] = human->Top;
				drawData.Equipments[2] = human->Arms;
				drawData.Equipments[3] = human->Legs;
				drawData.Equipments[4] = human->Feet;
				drawData.Equipments[5] = human->Ear;
				drawData.Equipments[6] = human->Neck;
				drawData.Equipments[7] = human->Wrist;
				drawData.Equipments[8] = human->RFinger;
				drawData.Equipments[9] = human->LFinger;
				drawData.Glasses[0] = human->Glasses0;
				drawData.Glasses[1] = human->Glasses1;
			}

			human->UpdateDrawData(&drawData, skipEquipmentArrays: !includeEquipment);

			if (includeEquipment)
			{
				// Force arm/feet slot skin materials to reload.
				human->SetupSlotModel(2);
				human->SetupSlotModel(4);
			}
		}
		catch
		{
			// Ignore.
		}
	}

	private static unsafe void TryApplyFrameOverrides(ICharacter character, byte height, byte skinTone)
	{
		try
		{
			if (!TryGetHuman(character, out SceneHuman* human))
			{
				return;
			}

			*(float*)((byte*)human + ModelScaleOffset) = Math.Clamp(height / 50f, 0.1f, 2.0f);
			TryWriteSkinShader(human, skinTone);
		}
		catch
		{
			// Ignore.
		}
	}

	private static unsafe void TryWriteSkinShader(SceneHuman* human, byte skinTone)
	{
		ConstantBuffer* cbuf = human->CustomizeParameterCBuffer;
		if (cbuf == null)
		{
			return;
		}

		Span<CustomizeParameter> buffer = cbuf->TryGetBuffer<CustomizeParameter>();
		if (buffer.IsEmpty)
		{
			return;
		}

		ref CustomizeParameter param = ref buffer[0];
		float muscle = param.SkinColor.W;
		(float r, float g, float b) = SkinToneToLinearRgb(skinTone);
		param.SkinColor = new Vector4(r * r, g * g, b * b, muscle);
	}

	private static (float R, float G, float B) SkinToneToLinearRgb(byte tone)
	{
		float t = tone / 255f;
		float r = 0.96f - (t * 0.62f);
		float g = 0.82f - (t * 0.58f);
		float b = 0.72f - (t * 0.52f);
		return (r, g, b);
	}

	private static void CopyCosmetic(ReadOnlySpan<byte> source, Span<byte> destination)
	{
		// Full customize block (0x1A) so race/clan redraws see every identity byte.
		int length = Math.Min(Math.Min(source.Length, destination.Length), CustomizeOffsets.Size);
		if (length <= 0)
		{
			return;
		}

		source[..length].CopyTo(destination[..length]);
	}
}
