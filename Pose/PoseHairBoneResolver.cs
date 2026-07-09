// © DeadlyDeathAngel.
// Licensed under the MIT license.

namespace LuminusBridge.Pose;

using LuminusBridge.Api;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

/// <summary>Maps pose hair bone names onto the target actor skeleton (mod hairstyles).</summary>
public static partial class PoseHairBoneResolver
{
	private static readonly Dictionary<string, (string Suffix, string Fallback)> HairAutoMap = new(StringComparer.Ordinal)
	{
		["HairAutoFrontLeft"] = ("l", "j_kami_f_l"),
		["HairAutoFrontRight"] = ("r", "j_kami_f_r"),
		["HairAutoA"] = ("a", "j_kami_a"),
		["HairAutoB"] = ("b", "j_kami_b"),
		["HairFront"] = ("f", string.Empty),
	};

	public static string? ResolveTargetBoneName(
		string poseBoneName,
		IReadOnlyDictionary<string, BoneTransformDto> skeletonBones)
	{
		string modern = LegacyBoneNameConverter.GetModernName(poseBoneName) ?? poseBoneName;
		if (skeletonBones.ContainsKey(modern) || skeletonBones.ContainsKey(poseBoneName))
		{
			return skeletonBones.ContainsKey(modern) ? modern : poseBoneName;
		}

		if (HairAutoMap.TryGetValue(poseBoneName, out (string Suffix, string Fallback) auto))
		{
			string? patternMatch = FindHairBoneBySuffix(auto.Suffix, skeletonBones);
			if (patternMatch != null)
			{
				return patternMatch;
			}

			if (!string.IsNullOrEmpty(auto.Fallback) && skeletonBones.ContainsKey(auto.Fallback))
			{
				return auto.Fallback;
			}
		}

		if (TryResolveExHairBone(modern, skeletonBones, out string? exHair))
		{
			return exHair;
		}

		if (modern.StartsWith("j_kami", StringComparison.Ordinal))
		{
			string suffix = modern.Length > 7 ? modern[7..] : string.Empty;
			string? patternMatch = FindHairBoneBySuffix(suffix, skeletonBones);
			if (patternMatch != null)
			{
				return patternMatch;
			}
		}

		return null;
	}

	private static string? FindHairBoneBySuffix(string suffix, IReadOnlyDictionary<string, BoneTransformDto> skeletonBones)
	{
		Regex regex = ExHairSuffixRegex(suffix);
		foreach (string name in skeletonBones.Keys)
		{
			if (regex.IsMatch(name))
			{
				return name;
			}
		}

		return null;
	}

	private static bool TryResolveExHairBone(
		string modernBoneName,
		IReadOnlyDictionary<string, BoneTransformDto> skeletonBones,
		out string? resolved)
	{
		resolved = null;
		Match match = ExHairBoneRegex().Match(modernBoneName);
		if (!match.Success)
		{
			return false;
		}

		string tail = match.Groups[1].Value;
		resolved = FindHairBoneBySuffix(tail, skeletonBones);
		return resolved != null;
	}

	[GeneratedRegex(@"^j_ex_h\d{4}_(.+)$", RegexOptions.CultureInvariant)]
	private static partial Regex ExHairBoneRegex();

	private static Regex ExHairSuffixRegex(string suffix)
		=> new($@"^j_ex_h\d{{4}}_{Regex.Escape(suffix)}$", RegexOptions.CultureInvariant);
}
