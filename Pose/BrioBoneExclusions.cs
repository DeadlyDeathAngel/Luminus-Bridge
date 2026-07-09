// © Luminus.
// Licensed under the MIT license.

namespace LuminusBridge.Pose;

using System;

/// <summary>Brio DefaultImporterOptions excludes weapon and ex (iv_) bones.</summary>
public static class BrioBoneExclusions
{
	public static bool IsExcluded(string boneName)
	{
		string modern = LegacyBoneNameConverter.GetModernName(boneName) ?? boneName;
		if (string.Equals(modern, "n_throw", StringComparison.Ordinal))
		{
			return true;
		}

		if (modern.StartsWith("iv_", StringComparison.Ordinal))
		{
			return true;
		}

		return IsWeaponBone(modern);
	}

	private static bool IsWeaponBone(string boneName)
		=> string.Equals(boneName, "n_buki_l", StringComparison.Ordinal)
			|| string.Equals(boneName, "n_buki_r", StringComparison.Ordinal)
			|| boneName.StartsWith("n_buki_tate_", StringComparison.Ordinal)
			|| boneName.StartsWith("j_buki2_", StringComparison.Ordinal)
			|| boneName.StartsWith("j_buki_kosi_", StringComparison.Ordinal)
			|| boneName.StartsWith("j_buki_sebo_", StringComparison.Ordinal);
}
