// © DeadlyDeathAngel.
// Licensed under the MIT license.

namespace AnamnesisBridge.Services;

/// <summary>
/// Raw customize byte offsets (matches Anamnesis ActorCustomizeMemory / game CustomizeData).
/// </summary>
internal static class CustomizeOffsets
{
	public const int Size = 0x1A;

	public const int Race = 0x00;
	public const int Gender = 0x01;
	public const int Age = 0x02;
	public const int Height = 0x03;
	public const int Tribe = 0x04;
	public const int Head = 0x05;
	public const int Hair = 0x06;
	public const int HighlightType = 0x07;
	public const int Skintone = 0x08;
	public const int REyeColor = 0x09;
	public const int HairTone = 0x0A;
	public const int Highlights = 0x0B;
	public const int FacialFeatures = 0x0C;
	public const int FacialFeatureColor = 0x0D;
	public const int Eyebrows = 0x0E;
	public const int LEyeColor = 0x0F;
	public const int Eyes = 0x10;
	public const int Nose = 0x11;
	public const int Jaw = 0x12;
	public const int Mouth = 0x13;
	public const int LipsToneFurPattern = 0x14;
	public const int EarMuscleTailSize = 0x15;
	public const int TailEarsType = 0x16;
	public const int Bust = 0x17;
	public const int FacePaint = 0x18;
	public const int FacePaintColor = 0x19;
}
