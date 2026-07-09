// © DeadlyDeathAngel.
// Licensed under the MIT license.

namespace LuminusBridge.Services;

/// <summary>
/// Maps tribe/gender/age to chara/human data path IDs for asset existence checks.
/// Mirrors desktop <see cref="Luminus.GameData.DataPathResolver"/>.
/// </summary>
internal static class BridgeDataPathResolver
{
	public static short? ToDataPath(byte tribe, byte gender, byte age)
	{
		bool isNpc = age != 1;
		if (!TryBasePath(tribe, gender, out short basePath))
		{
			return null;
		}

		return isNpc ? (short)(basePath + 3) : basePath;
	}

	public static string ResolveTailEarsPath(short raceSexId, ushort tailEarsId)
	{
		string type = "tail";
		string typePrefix = "t";
		string typeSuffix = "til";

		if (raceSexId is 1701 or 1704 or 1801 or 1804)
		{
			type = "zear";
			typePrefix = "z";
			typeSuffix = "zer";
		}

		return $"chara/human/c{raceSexId:D4}/obj/{type}/{typePrefix}{tailEarsId:D4}/model/c{raceSexId:D4}{typePrefix}{tailEarsId:D4}_{typeSuffix}.mdl";
	}

	private static bool TryBasePath(byte tribe, byte gender, out short basePath)
	{
		basePath = 0;
		return (tribe, gender) switch
		{
			(1, 0) => Set(101, out basePath),
			(1, 1) => Set(201, out basePath),
			(2, 0) => Set(301, out basePath),
			(2, 1) => Set(401, out basePath),
			(3, 0) or (4, 0) => Set(501, out basePath),
			(3, 1) or (4, 1) => Set(601, out basePath),
			(5, 0) or (6, 0) => Set(1101, out basePath),
			(5, 1) or (6, 1) => Set(1201, out basePath),
			(7, 0) or (8, 0) => Set(701, out basePath),
			(7, 1) or (8, 1) => Set(801, out basePath),
			(9, 0) or (10, 0) => Set(901, out basePath),
			(9, 1) or (10, 1) => Set(1001, out basePath),
			(11, 0) or (12, 0) => Set(1301, out basePath),
			(11, 1) or (12, 1) => Set(1401, out basePath),
			(13, 0) or (14, 0) => Set(1501, out basePath),
			(13, 1) or (14, 1) => Set(1601, out basePath),
			(15, 0) or (16, 0) => Set(1701, out basePath),
			(15, 1) or (16, 1) => Set(1801, out basePath),
			_ => false,
		};
	}

	private static bool Set(short value, out short basePath)
	{
		basePath = value;
		return true;
	}
}
