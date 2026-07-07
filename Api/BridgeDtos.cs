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

	[JsonPropertyName("faceCameraEnabled")]
	public bool FaceCameraEnabled { get; init; }

	[JsonPropertyName("gazeCameraEnabled")]
	public bool GazeCameraEnabled { get; init; }
}

public sealed class GposePrepareResponse
{
	[JsonPropertyName("ok")]
	public bool Ok { get; init; }

	[JsonPropertyName("disabledFaceCamera")]
	public bool DisabledFaceCamera { get; init; }

	[JsonPropertyName("disabledGazeCamera")]
	public bool DisabledGazeCamera { get; init; }

	[JsonPropertyName("faceCameraEnabled")]
	public bool FaceCameraEnabled { get; init; }

	[JsonPropertyName("gazeCameraEnabled")]
	public bool GazeCameraEnabled { get; init; }

	[JsonPropertyName("error")]
	public string? Error { get; init; }
}

public sealed class CameraResponse
{
	[JsonPropertyName("ok")]
	public bool Ok { get; init; }

	[JsonPropertyName("available")]
	public bool Available { get; init; }

	[JsonPropertyName("isInGpose")]
	public bool IsInGpose { get; init; }

	[JsonPropertyName("delimitCamera")]
	public bool DelimitCamera { get; init; }

	[JsonPropertyName("zoom")]
	public float Zoom { get; init; }

	[JsonPropertyName("minZoom")]
	public float MinZoom { get; init; }

	[JsonPropertyName("maxZoom")]
	public float MaxZoom { get; init; }

	[JsonPropertyName("fieldOfView")]
	public float FieldOfView { get; init; }

	[JsonPropertyName("angleXDeg")]
	public float AngleXDeg { get; init; }

	[JsonPropertyName("angleYDeg")]
	public float AngleYDeg { get; init; }

	[JsonPropertyName("rotationDeg")]
	public float RotationDeg { get; init; }

	[JsonPropertyName("panXDeg")]
	public float PanXDeg { get; init; }

	[JsonPropertyName("panYDeg")]
	public float PanYDeg { get; init; }

	[JsonPropertyName("positionX")]
	public float PositionX { get; init; }

	[JsonPropertyName("positionY")]
	public float PositionY { get; init; }

	[JsonPropertyName("positionZ")]
	public float PositionZ { get; init; }

	[JsonPropertyName("error")]
	public string? Error { get; init; }
}

public sealed class CameraUpdateRequest
{
	[JsonPropertyName("delimitCamera")]
	public bool? DelimitCamera { get; init; }

	[JsonPropertyName("zoom")]
	public float? Zoom { get; init; }

	[JsonPropertyName("fieldOfView")]
	public float? FieldOfView { get; init; }

	[JsonPropertyName("angleXDeg")]
	public float? AngleXDeg { get; init; }

	[JsonPropertyName("angleYDeg")]
	public float? AngleYDeg { get; init; }

	[JsonPropertyName("rotationDeg")]
	public float? RotationDeg { get; init; }

	[JsonPropertyName("panXDeg")]
	public float? PanXDeg { get; init; }

	[JsonPropertyName("panYDeg")]
	public float? PanYDeg { get; init; }

	[JsonPropertyName("positionX")]
	public float? PositionX { get; init; }

	[JsonPropertyName("positionY")]
	public float? PositionY { get; init; }

	[JsonPropertyName("positionZ")]
	public float? PositionZ { get; init; }
}

public sealed class CameraShotDto
{
	[JsonPropertyName("delimitCamera")]
	public bool DelimitCamera { get; init; }

	[JsonPropertyName("zoom")]
	public float Zoom { get; init; }

	[JsonPropertyName("fieldOfView")]
	public float FieldOfView { get; init; }

	[JsonPropertyName("panX")]
	public float PanX { get; init; }

	[JsonPropertyName("panY")]
	public float PanY { get; init; }

	[JsonPropertyName("positionX")]
	public float PositionX { get; init; }

	[JsonPropertyName("positionY")]
	public float PositionY { get; init; }

	[JsonPropertyName("positionZ")]
	public float PositionZ { get; init; }

	[JsonPropertyName("rotationX")]
	public float RotationX { get; init; }

	[JsonPropertyName("rotationY")]
	public float RotationY { get; init; }

	[JsonPropertyName("rotationZ")]
	public float RotationZ { get; init; }
}

public sealed class CameraShotApplyRequest
{
	[JsonPropertyName("objectIndex")]
	public int ObjectIndex { get; init; }

	[JsonPropertyName("shot")]
	public CameraShotDto Shot { get; init; } = new();
}

public sealed class CameraShotResponse
{
	[JsonPropertyName("ok")]
	public bool Ok { get; init; }

	[JsonPropertyName("objectIndex")]
	public int ObjectIndex { get; init; }

	[JsonPropertyName("shot")]
	public CameraShotDto? Shot { get; init; }

	[JsonPropertyName("error")]
	public string? Error { get; init; }
}

public sealed class WorldResponse
{
	[JsonPropertyName("ok")]
	public bool Ok { get; init; }

	[JsonPropertyName("available")]
	public bool Available { get; init; }

	[JsonPropertyName("isInGpose")]
	public bool IsInGpose { get; init; }

	[JsonPropertyName("timeOfDayMinutes")]
	public long TimeOfDayMinutes { get; init; }

	[JsonPropertyName("timeString")]
	public string TimeString { get; init; } = "00:00";

	[JsonPropertyName("dayOfMonth")]
	public byte DayOfMonth { get; init; }

	[JsonPropertyName("weatherId")]
	public ushort WeatherId { get; init; }

	[JsonPropertyName("weatherName")]
	public string WeatherName { get; init; } = string.Empty;

	[JsonPropertyName("weatherIconId")]
	public uint WeatherIconId { get; init; }

	[JsonPropertyName("freezeTime")]
	public bool FreezeTime { get; init; }

	[JsonPropertyName("holdWeather")]
	public bool HoldWeather { get; init; }

	[JsonPropertyName("error")]
	public string? Error { get; init; }
}

public sealed class WorldUpdateRequest
{
	[JsonPropertyName("timeOfDayMinutes")]
	public long? TimeOfDayMinutes { get; init; }

	[JsonPropertyName("dayOfMonth")]
	public byte? DayOfMonth { get; init; }

	[JsonPropertyName("weatherId")]
	public ushort? WeatherId { get; init; }

	[JsonPropertyName("freezeTime")]
	public bool? FreezeTime { get; init; }

	[JsonPropertyName("holdWeather")]
	public bool? HoldWeather { get; init; }
}

public sealed class MotionResponse
{
	[JsonPropertyName("ok")]
	public bool Ok { get; init; }

	[JsonPropertyName("objectIndex")]
	public int ObjectIndex { get; init; }

	[JsonPropertyName("motionDisabled")]
	public bool MotionDisabled { get; init; }

	[JsonPropertyName("isMotionEnabled")]
	public bool IsMotionEnabled { get; init; }

	[JsonPropertyName("isInGpose")]
	public bool IsInGpose { get; init; }

	[JsonPropertyName("error")]
	public string? Error { get; init; }
}

public sealed class MotionUpdateRequest
{
	[JsonPropertyName("motionDisabled")]
	public bool? MotionDisabled { get; init; }
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
	public bool ApplyPosition { get; init; }

	[JsonPropertyName("applyRotation")]
	public bool ApplyRotation { get; init; } = true;

	[JsonPropertyName("applyScale")]
	public bool ApplyScale { get; init; }

	/// <summary>After applying face bones, restore j_kao to its pre-apply transform (desktop expression hack).</summary>
	[JsonPropertyName("restoreHeadAfterApply")]
	public bool RestoreHeadAfterApply { get; init; }

	/// <summary>Atomically apply body then expression passes on one framework tick (desktop parity).</summary>
	[JsonPropertyName("characterTwoPass")]
	public bool CharacterTwoPass { get; init; }

	[JsonPropertyName("brioStyleBodyPass")]
	public bool BrioStyleBodyPass { get; init; }

	[JsonPropertyName("applyModelTransform")]
	public bool ApplyModelTransform { get; init; }

	[JsonPropertyName("modelDiffPosX")]
	public float? ModelDiffPosX { get; init; }

	[JsonPropertyName("modelDiffPosY")]
	public float? ModelDiffPosY { get; init; }

	[JsonPropertyName("modelDiffPosZ")]
	public float? ModelDiffPosZ { get; init; }

	[JsonPropertyName("modelDiffRotX")]
	public float? ModelDiffRotX { get; init; }

	[JsonPropertyName("modelDiffRotY")]
	public float? ModelDiffRotY { get; init; }

	[JsonPropertyName("modelDiffRotZ")]
	public float? ModelDiffRotZ { get; init; }

	[JsonPropertyName("modelDiffRotW")]
	public float? ModelDiffRotW { get; init; }

	[JsonPropertyName("modelDiffScaleX")]
	public float? ModelDiffScaleX { get; init; }

	[JsonPropertyName("modelDiffScaleY")]
	public float? ModelDiffScaleY { get; init; }

	[JsonPropertyName("modelDiffScaleZ")]
	public float? ModelDiffScaleZ { get; init; }
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

	[JsonPropertyName("equipLevel")]
	public byte EquipLevel { get; init; }

	[JsonPropertyName("itemLevel")]
	public ushort ItemLevel { get; init; }

	[JsonPropertyName("uiCategory")]
	public string UiCategory { get; init; } = string.Empty;

	[JsonPropertyName("description")]
	public string Description { get; init; } = string.Empty;

	[JsonPropertyName("category")]
	public int Category { get; init; }

	[JsonPropertyName("equipableClasses")]
	public long EquipableClasses { get; init; }

	[JsonPropertyName("equipRaceMask")]
	public ushort EquipRaceMask { get; init; }

	[JsonPropertyName("subSet")]
	public ushort SubSet { get; init; }

	[JsonPropertyName("subBase")]
	public ushort SubBase { get; init; }

	[JsonPropertyName("subVariant")]
	public ushort SubVariant { get; init; }

	[JsonPropertyName("isModded")]
	public bool IsModded { get; init; }

	[JsonPropertyName("modPack")]
	public string ModPack { get; init; } = string.Empty;

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

public sealed class WeatherDto
{
	[JsonPropertyName("id")]
	public ushort Id { get; init; }

	[JsonPropertyName("name")]
	public string Name { get; init; } = string.Empty;

	[JsonPropertyName("iconId")]
	public uint IconId { get; init; }
}

public sealed class WeathersResponse
{
	[JsonPropertyName("ok")]
	public bool Ok { get; init; } = true;

	[JsonPropertyName("weathers")]
	public IReadOnlyList<WeatherDto> Weathers { get; init; } = [];
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

	/// <summary>Posing enabled and hook detours are actively blocking Havok (past camera/GPose settle).</summary>
	[JsonPropertyName("posingHooksEngaged")]
	public bool PosingHooksEngaged { get; init; }
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
