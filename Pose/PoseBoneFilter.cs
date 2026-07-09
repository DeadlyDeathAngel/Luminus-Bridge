// © Luminus.
// Licensed under the MIT license.

namespace LuminusBridge.Pose;

using System;

/// <summary>Matches desktop Luminus / Linux client pose import scopes.</summary>
public enum PoseImportScope
{
	BodyOnly,
	ExpressionOnly,
}

/// <summary>Filters pose file bone names by import scope.</summary>
public static class PoseBoneFilter
{
	public static bool IsHairRigBone(string boneName)
	{
		string modern = LegacyBoneNameConverter.GetModernName(boneName) ?? boneName;
		return IsHairBone(modern);
	}

	public static bool ShouldInclude(string boneName, PoseImportScope scope, bool brioStyleBodyPass = false)
	{
		string modern = LegacyBoneNameConverter.GetModernName(boneName) ?? boneName;
		if (string.Equals(modern, "n_root", StringComparison.Ordinal))
		{
			return false;
		}

		if (brioStyleBodyPass && BrioBoneExclusions.IsExcluded(modern))
		{
			return false;
		}

		return scope switch
		{
			PoseImportScope.BodyOnly => IsBodyBone(modern, brioStyleBodyPass),
			PoseImportScope.ExpressionOnly => IsExpressionBone(modern),
			_ => true,
		};
	}

	private static bool IsBodyBone(string boneName, bool brioStyleBodyPass = false)
	{
		if (brioStyleBodyPass && string.Equals(boneName, "j_kao", StringComparison.Ordinal))
		{
			return false;
		}

		return string.Equals(boneName, "j_kao", StringComparison.Ordinal)
			|| (!IsFaceBone(boneName) && !IsHairBone(boneName) && !IsEarBone(boneName));
	}

	private static bool IsExpressionBone(string boneName)
		=> IsFaceBone(boneName) || IsEarBone(boneName) || IsHairBone(boneName);

	private static bool IsHairBone(string boneName)
		=> boneName.StartsWith("j_kami", StringComparison.Ordinal)
			|| boneName.StartsWith("j_ex_h", StringComparison.Ordinal)
			|| string.Equals(boneName, "j_ex_met_va", StringComparison.Ordinal);

	private static bool IsEarBone(string boneName)
		=> boneName.StartsWith("j_mimi", StringComparison.Ordinal)
			|| boneName.StartsWith("j_zera", StringComparison.Ordinal)
			|| boneName.StartsWith("j_zerb", StringComparison.Ordinal)
			|| boneName.StartsWith("j_zerc", StringComparison.Ordinal)
			|| boneName.StartsWith("j_zerd", StringComparison.Ordinal);

	private static bool IsFaceBone(string boneName)
		=> boneName.StartsWith("j_f_", StringComparison.Ordinal)
			|| boneName.StartsWith("n_f_", StringComparison.Ordinal)
			|| string.Equals(boneName, "j_kao", StringComparison.Ordinal)
			|| string.Equals(boneName, "j_ago", StringComparison.Ordinal);
}
