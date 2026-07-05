// © Anamnesis.
// Licensed under the MIT license.

namespace AnamnesisBridge.Api;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

public sealed class HealthResponse
{
	[JsonPropertyName("ok")]
	public bool Ok { get; init; } = true;

	[JsonPropertyName("plugin")]
	public string Plugin { get; init; } = "AnamnesisBridge";

	[JsonPropertyName("version")]
	public string Version { get; init; } = Services.BridgeVersion.Current;

	[JsonPropertyName("api")]
	public int Api { get; init; } = 2;

	[JsonPropertyName("ipc")]
	public bool Ipc { get; init; } = true;

	[JsonPropertyName("signedIn")]
	public bool SignedIn { get; init; }

	[JsonPropertyName("isInGpose")]
	public bool IsInGpose { get; init; }

	[JsonPropertyName("territoryId")]
	public uint TerritoryId { get; init; }

	[JsonPropertyName("listening")]
	public bool Listening { get; init; } = true;
}

public sealed class GposeResponse
{
	[JsonPropertyName("isInGpose")]
	public bool IsInGpose { get; init; }

	[JsonPropertyName("source")]
	public string Source { get; init; } = "IClientState.IsGPosing";
}

public sealed class TerritoryResponse
{
	[JsonPropertyName("territoryId")]
	public uint TerritoryId { get; init; }

	[JsonPropertyName("signedIn")]
	public bool SignedIn { get; init; }
}

public sealed class StatusResponse
{
	[JsonPropertyName("isInGpose")]
	public bool IsInGpose { get; init; }

	[JsonPropertyName("territoryId")]
	public uint TerritoryId { get; init; }

	[JsonPropertyName("signedIn")]
	public bool SignedIn { get; init; }

	[JsonPropertyName("frameworkTick")]
	public bool FrameworkTick { get; init; }
}

public sealed class ErrorResponse
{
	[JsonPropertyName("ok")]
	public bool Ok { get; init; }

	[JsonPropertyName("error")]
	public string Error { get; init; } = string.Empty;
}

public sealed class ActorsResponse
{
	[JsonPropertyName("actors")]
	public IReadOnlyList<BridgeActorDto> Actors { get; init; } = [];

	[JsonPropertyName("source")]
	public string Source { get; init; } = "IObjectTable";
}

public sealed class BridgeActorDto
{
	[JsonPropertyName("objectIndex")]
	public int ObjectIndex { get; init; }

	[JsonPropertyName("address")]
	public string Address { get; init; } = string.Empty;

	[JsonPropertyName("name")]
	public string Name { get; init; } = string.Empty;

	[JsonPropertyName("dataId")]
	public uint DataId { get; init; }

	[JsonPropertyName("objectKind")]
	public byte ObjectKind { get; init; }

	[JsonPropertyName("distance")]
	public double Distance { get; init; }

	[JsonPropertyName("isLocalPlayer")]
	public bool IsLocalPlayer { get; init; }

	[JsonPropertyName("isGposeActor")]
	public bool IsGposeActor { get; init; }

	[JsonPropertyName("homeWorldId")]
	public uint HomeWorldId { get; init; }

	[JsonPropertyName("homeWorld")]
	public string HomeWorld { get; init; } = string.Empty;
}

public sealed class TargetSetRequest
{
	[JsonPropertyName("objectIndex")]
	public int ObjectIndex { get; init; }
}

public sealed class TargetSetResponse
{
	[JsonPropertyName("ok")]
	public bool Ok { get; init; }

	[JsonPropertyName("error")]
	public string? Error { get; init; }
}

public sealed class ActorAppearanceDto
{
	[JsonPropertyName("objectIndex")]
	public int ObjectIndex { get; init; }

	[JsonPropertyName("name")]
	public string Name { get; init; } = string.Empty;

	[JsonPropertyName("homeWorldId")]
	public uint HomeWorldId { get; init; }

	[JsonPropertyName("homeWorld")]
	public string HomeWorld { get; init; } = string.Empty;

	[JsonPropertyName("level")]
	public byte Level { get; init; }

	[JsonPropertyName("classJob")]
	public string ClassJob { get; init; } = string.Empty;

	[JsonPropertyName("race")]
	public byte Race { get; init; }

	[JsonPropertyName("sex")]
	public byte Sex { get; init; }

	[JsonPropertyName("tribe")]
	public byte Tribe { get; init; }

	[JsonPropertyName("bodyType")]
	public byte BodyType { get; init; }

	[JsonPropertyName("height")]
	public byte Height { get; init; }

	[JsonPropertyName("face")]
	public byte Face { get; init; }

	[JsonPropertyName("hairstyle")]
	public byte Hairstyle { get; init; }

	[JsonPropertyName("hairColor")]
	public byte HairColor { get; init; }

	[JsonPropertyName("hairColor2")]
	public byte HairColor2 { get; init; }

	[JsonPropertyName("skinColor")]
	public byte SkinColor { get; init; }

	[JsonPropertyName("eyeColor")]
	public byte EyeColor { get; init; }

	[JsonPropertyName("eyeColorRight")]
	public byte EyeColorRight { get; init; }

	// Full customize block (control plane 0.2.5.0+).
	[JsonPropertyName("age")]
	public byte Age { get; init; }

	[JsonPropertyName("highlightType")]
	public byte HighlightType { get; init; }

	[JsonPropertyName("facialFeatures")]
	public byte FacialFeatures { get; init; }

	[JsonPropertyName("facialFeatureColor")]
	public byte FacialFeatureColor { get; init; }

	[JsonPropertyName("eyebrows")]
	public byte Eyebrows { get; init; }

	[JsonPropertyName("eyes")]
	public byte Eyes { get; init; }

	[JsonPropertyName("nose")]
	public byte Nose { get; init; }

	[JsonPropertyName("jaw")]
	public byte Jaw { get; init; }

	[JsonPropertyName("mouth")]
	public byte Mouth { get; init; }

	[JsonPropertyName("lipsToneFurPattern")]
	public byte LipsToneFurPattern { get; init; }

	[JsonPropertyName("earMuscleTailSize")]
	public byte EarMuscleTailSize { get; init; }

	[JsonPropertyName("tailEarsType")]
	public byte TailEarsType { get; init; }

	[JsonPropertyName("bust")]
	public byte Bust { get; init; }

	[JsonPropertyName("facePaint")]
	public byte FacePaint { get; init; }

	[JsonPropertyName("facePaintColor")]
	public byte FacePaintColor { get; init; }
}

public sealed class CapabilitiesResponse
{
	[JsonPropertyName("ok")]
	public bool Ok { get; init; } = true;

	[JsonPropertyName("version")]
	public string Version { get; init; } = Services.BridgeVersion.Current;

	[JsonPropertyName("step")]
	public string Step { get; init; } = "control-plane";

	[JsonPropertyName("api")]
	public int Api { get; init; } = 3;

	[JsonPropertyName("capabilities")]
	public IReadOnlyList<string> Capabilities { get; init; } = [];
}

public sealed class BoneTransformDto
{
	[JsonPropertyName("name")]
	public string Name { get; init; } = string.Empty;

	[JsonPropertyName("partial")]
	public int Partial { get; init; }

	[JsonPropertyName("index")]
	public int Index { get; init; }

	[JsonPropertyName("depth")]
	public int Depth { get; init; }

	[JsonPropertyName("posX")]
	public float PosX { get; init; }

	[JsonPropertyName("posY")]
	public float PosY { get; init; }

	[JsonPropertyName("posZ")]
	public float PosZ { get; init; }

	[JsonPropertyName("rotX")]
	public float RotX { get; init; }

	[JsonPropertyName("rotY")]
	public float RotY { get; init; }

	[JsonPropertyName("rotZ")]
	public float RotZ { get; init; }

	[JsonPropertyName("rotW")]
	public float RotW { get; init; }

	[JsonPropertyName("scaleX")]
	public float ScaleX { get; init; }

	[JsonPropertyName("scaleY")]
	public float ScaleY { get; init; }

	[JsonPropertyName("scaleZ")]
	public float ScaleZ { get; init; }
}

public sealed class SkeletonResponse
{
	[JsonPropertyName("ok")]
	public bool Ok { get; init; }

	[JsonPropertyName("objectIndex")]
	public int ObjectIndex { get; init; }

	[JsonPropertyName("boneCount")]
	public int BoneCount { get; init; }

	[JsonPropertyName("bones")]
	public IReadOnlyList<BoneTransformDto> Bones { get; init; } = [];

	[JsonPropertyName("error")]
	public string? Error { get; init; }
}

public sealed class SetBoneTransformRequest
{
	[JsonPropertyName("partial")]
	public int Partial { get; init; }

	[JsonPropertyName("index")]
	public int Index { get; init; }

	[JsonPropertyName("posX")]
	public float PosX { get; init; }

	[JsonPropertyName("posY")]
	public float PosY { get; init; }

	[JsonPropertyName("posZ")]
	public float PosZ { get; init; }

	[JsonPropertyName("rotX")]
	public float RotX { get; init; }

	[JsonPropertyName("rotY")]
	public float RotY { get; init; }

	[JsonPropertyName("rotZ")]
	public float RotZ { get; init; }

	[JsonPropertyName("rotW")]
	public float RotW { get; init; } = 1f;

	[JsonPropertyName("scaleX")]
	public float ScaleX { get; init; } = 1f;

	[JsonPropertyName("scaleY")]
	public float ScaleY { get; init; } = 1f;

	[JsonPropertyName("scaleZ")]
	public float ScaleZ { get; init; } = 1f;
}

public sealed class SetBoneTransformResponse
{
	[JsonPropertyName("ok")]
	public bool Ok { get; init; }

	[JsonPropertyName("objectIndex")]
	public int ObjectIndex { get; init; }

	[JsonPropertyName("partial")]
	public int Partial { get; init; }

	[JsonPropertyName("index")]
	public int Index { get; init; }

	[JsonPropertyName("error")]
	public string? Error { get; init; }
}

public sealed class ApplyPoseBoneDto
{
	[JsonPropertyName("posX")]
	public float? PosX { get; init; }

	[JsonPropertyName("posY")]
	public float? PosY { get; init; }

	[JsonPropertyName("posZ")]
	public float? PosZ { get; init; }

	[JsonPropertyName("rotX")]
	public float? RotX { get; init; }

	[JsonPropertyName("rotY")]
	public float? RotY { get; init; }

	[JsonPropertyName("rotZ")]
	public float? RotZ { get; init; }

	[JsonPropertyName("rotW")]
	public float? RotW { get; init; }

	[JsonPropertyName("scaleX")]
	public float? ScaleX { get; init; }

	[JsonPropertyName("scaleY")]
	public float? ScaleY { get; init; }

	[JsonPropertyName("scaleZ")]
	public float? ScaleZ { get; init; }
}

public sealed class ApplyPoseRequest
{
	[JsonPropertyName("bones")]
	public Dictionary<string, ApplyPoseBoneDto> Bones { get; init; } = new(StringComparer.Ordinal);

	[JsonPropertyName("applyPosition")]
	public bool ApplyPosition { get; init; } = true;

	[JsonPropertyName("applyRotation")]
	public bool ApplyRotation { get; init; } = true;

	[JsonPropertyName("applyScale")]
	public bool ApplyScale { get; init; } = true;
}

public sealed class ApplyPoseResponse
{
	[JsonPropertyName("ok")]
	public bool Ok { get; init; }

	[JsonPropertyName("objectIndex")]
	public int ObjectIndex { get; init; }

	[JsonPropertyName("appliedCount")]
	public int AppliedCount { get; init; }

	[JsonPropertyName("skippedCount")]
	public int SkippedCount { get; init; }

	[JsonPropertyName("error")]
	public string? Error { get; init; }
}

public sealed class EquipmentSlotDto
{
	[JsonPropertyName("slot")]
	public string Slot { get; init; } = string.Empty;

	[JsonPropertyName("set")]
	public ushort Set { get; init; }

	[JsonPropertyName("variant")]
	public byte Variant { get; init; }

	[JsonPropertyName("dye")]
	public byte Dye { get; init; }

	[JsonPropertyName("dye2")]
	public byte Dye2 { get; init; }

	[JsonPropertyName("itemName")]
	public string ItemName { get; init; } = string.Empty;

	[JsonPropertyName("dyeName")]
	public string DyeName { get; init; } = string.Empty;

	[JsonPropertyName("dye2Name")]
	public string Dye2Name { get; init; } = string.Empty;

	[JsonPropertyName("dyeHex")]
	public string DyeHex { get; init; } = "#00000000";

	[JsonPropertyName("dye2Hex")]
	public string Dye2Hex { get; init; } = "#00000000";

	[JsonPropertyName("iconId")]
	public uint IconId { get; init; }
}

public sealed class WeaponSlotDto
{
	[JsonPropertyName("slot")]
	public string Slot { get; init; } = string.Empty;

	[JsonPropertyName("set")]
	public ushort Set { get; init; }

	[JsonPropertyName("base")]
	public ushort Base { get; init; }

	[JsonPropertyName("variant")]
	public ushort Variant { get; init; }

	[JsonPropertyName("dye")]
	public byte Dye { get; init; }

	[JsonPropertyName("dye2")]
	public byte Dye2 { get; init; }

	[JsonPropertyName("itemName")]
	public string ItemName { get; init; } = string.Empty;

	[JsonPropertyName("dyeName")]
	public string DyeName { get; init; } = string.Empty;

	[JsonPropertyName("dye2Name")]
	public string Dye2Name { get; init; } = string.Empty;

	[JsonPropertyName("dyeHex")]
	public string DyeHex { get; init; } = "#00000000";

	[JsonPropertyName("dye2Hex")]
	public string Dye2Hex { get; init; } = "#00000000";

	[JsonPropertyName("iconId")]
	public uint IconId { get; init; }
}

public sealed class CatalogItemDto
{
	[JsonPropertyName("itemId")]
	public uint ItemId { get; init; }

	[JsonPropertyName("name")]
	public string Name { get; init; } = string.Empty;

	[JsonPropertyName("set")]
	public ushort Set { get; init; }

	[JsonPropertyName("base")]
	public ushort Base { get; init; }

	[JsonPropertyName("variant")]
	public ushort Variant { get; init; }

	[JsonPropertyName("iconId")]
	public uint IconId { get; init; }

	public override string ToString() => this.Name;
}

public sealed class DyeDto
{
	[JsonPropertyName("id")]
	public byte Id { get; init; }

	[JsonPropertyName("name")]
	public string Name { get; init; } = string.Empty;

	[JsonPropertyName("hex")]
	public string Hex { get; init; } = "#00000000";

	public override string ToString() => this.Name;
}

public sealed class ColorEntryDto
{
	[JsonPropertyName("index")]
	public ushort Index { get; init; }

	[JsonPropertyName("hex")]
	public string Hex { get; init; } = "#000000";

	[JsonPropertyName("r")]
	public byte R { get; init; }

	[JsonPropertyName("g")]
	public byte G { get; init; }

	[JsonPropertyName("b")]
	public byte B { get; init; }

	[JsonPropertyName("skip")]
	public bool Skip { get; init; }
}

public sealed class CatalogItemsResponse
{
	[JsonPropertyName("ok")]
	public bool Ok { get; init; } = true;

	[JsonPropertyName("slot")]
	public string Slot { get; init; } = string.Empty;

	[JsonPropertyName("items")]
	public IReadOnlyList<CatalogItemDto> Items { get; init; } = [];
}

public sealed class DyesResponse
{
	[JsonPropertyName("ok")]
	public bool Ok { get; init; } = true;

	[JsonPropertyName("dyes")]
	public IReadOnlyList<DyeDto> Dyes { get; init; } = [];
}

public sealed class ColorsResponse
{
	[JsonPropertyName("ok")]
	public bool Ok { get; init; } = true;

	[JsonPropertyName("type")]
	public string Type { get; init; } = string.Empty;

	[JsonPropertyName("colors")]
	public IReadOnlyList<ColorEntryDto> Colors { get; init; } = [];
}

public sealed class CustomizeOptionDto
{
	[JsonPropertyName("id")]
	public byte Id { get; init; }

	[JsonPropertyName("name")]
	public string Name { get; init; } = string.Empty;

	[JsonPropertyName("iconId")]
	public uint IconId { get; init; }

	public override string ToString() => this.Name;
}

public sealed class CustomizeOptionsResponse
{
	[JsonPropertyName("ok")]
	public bool Ok { get; init; } = true;

	[JsonPropertyName("kind")]
	public string Kind { get; init; } = string.Empty;

	[JsonPropertyName("options")]
	public IReadOnlyList<CustomizeOptionDto> Options { get; init; } = [];
}

public sealed class ActorEquipmentDto
{
	[JsonPropertyName("objectIndex")]
	public int ObjectIndex { get; init; }

	[JsonPropertyName("name")]
	public string Name { get; init; } = string.Empty;

	[JsonPropertyName("slots")]
	public IReadOnlyList<EquipmentSlotDto> Slots { get; init; } = [];

	[JsonPropertyName("mainHand")]
	public WeaponSlotDto? MainHand { get; init; }

	[JsonPropertyName("offHand")]
	public WeaponSlotDto? OffHand { get; init; }

	[JsonPropertyName("glasses0")]
	public ushort Glasses0 { get; init; }

	[JsonPropertyName("glasses1")]
	public ushort Glasses1 { get; init; }
}

public sealed class EquipmentSetResponse
{
	[JsonPropertyName("ok")]
	public bool Ok { get; init; }

	[JsonPropertyName("error")]
	public string? Error { get; init; }
}

public sealed class AppearanceSetResponse
{
	[JsonPropertyName("ok")]
	public bool Ok { get; init; }

	[JsonPropertyName("error")]
	public string? Error { get; init; }
}

public sealed class RedrawResponse
{
	[JsonPropertyName("ok")]
	public bool Ok { get; init; }

	[JsonPropertyName("error")]
	public string? Error { get; init; }
}

public sealed class BridgeIpcStatusDto
{
	[JsonPropertyName("ok")]
	public bool Ok { get; init; }

	[JsonPropertyName("transport")]
	public string Transport { get; init; } = "anamnesis-bridge-http";

	[JsonPropertyName("signedIn")]
	public bool SignedIn { get; init; }

	[JsonPropertyName("isInGpose")]
	public bool IsInGpose { get; init; }

	[JsonPropertyName("posingEnabled")]
	public bool PosingEnabled { get; init; }

	[JsonPropertyName("freezePhysics")]
	public bool FreezePhysics { get; init; }

	[JsonPropertyName("freezeWorldVisualState")]
	public bool FreezeWorldVisualState { get; init; }

	[JsonPropertyName("capabilities")]
	public IReadOnlyList<string> Capabilities { get; init; } = [];

	[JsonPropertyName("posingHooksActive")]
	public bool PosingHooksActive { get; init; }
}

public sealed class BridgeIpcCommandRequest
{
	[JsonPropertyName("command")]
	public string Command { get; init; } = string.Empty;

	[JsonPropertyName("value")]
	public bool? Value { get; init; }
}

public sealed class BridgeIpcCommandResponse
{
	[JsonPropertyName("ok")]
	public bool Ok { get; init; }

	[JsonPropertyName("value")]
	public bool? Value { get; init; }

	[JsonPropertyName("error")]
	public string? Error { get; init; }
}
