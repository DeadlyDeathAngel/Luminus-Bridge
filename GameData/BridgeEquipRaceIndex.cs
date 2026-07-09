// © DeadlyDeathAngel.
// Licensed under the MIT license.

namespace LuminusBridge.GameData;

using System;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

internal static class BridgeEquipRaceIndex
{
	private static ushort[]? cache;

	public static void Initialize(IDataManager dataManager)
	{
		if (cache != null)
		{
			return;
		}

		cache = new ushort[256];
		var sheet = dataManager.Excel.GetSheet<EquipRaceCategory>();
		foreach (EquipRaceCategory row in sheet)
		{
			if (row.RowId >= cache.Length)
			{
				Array.Resize(ref cache, (int)row.RowId + 1);
			}

			cache[row.RowId] = Pack(row);
		}
	}

	public static ushort GetMask(byte restrictionId)
	{
		if (cache == null || restrictionId >= cache.Length)
		{
			return 0;
		}

		return cache[restrictionId];
	}

	private static ushort Pack(EquipRaceCategory row)
	{
		ushort mask = 0;
		if (row.Hyur)
		{
			mask |= 1 << 0;
		}

		if (row.Elezen)
		{
			mask |= 1 << 1;
		}

		if (row.Lalafell)
		{
			mask |= 1 << 2;
		}

		if (row.Miqote)
		{
			mask |= 1 << 3;
		}

		if (row.Roegadyn)
		{
			mask |= 1 << 4;
		}

		if (row.AuRa)
		{
			mask |= 1 << 5;
		}

		if (row.Hrothgar)
		{
			mask |= 1 << 6;
		}

		if (row.Viera)
		{
			mask |= 1 << 7;
		}

		if (row.Male)
		{
			mask |= 1 << 8;
		}

		if (row.Female)
		{
			mask |= 1 << 9;
		}

		return mask;
	}
}
