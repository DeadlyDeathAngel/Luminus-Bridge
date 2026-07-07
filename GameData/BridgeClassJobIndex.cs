// © DeadlyDeathAngel.
// Licensed under the MIT license.

namespace AnamnesisBridge.GameData;

using System;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

internal static class BridgeClassJobIndex
{
	private static long[]? cache;

	public static void Initialize(IDataManager dataManager)
	{
		if (cache != null)
		{
			return;
		}

		cache = new long[512];
		var sheet = dataManager.Excel.GetSheet<ClassJobCategory>();
		foreach (ClassJobCategory row in sheet)
		{
			if (row.RowId >= cache.Length)
			{
				Array.Resize(ref cache, (int)row.RowId + 1);
			}

			cache[row.RowId] = ToFlags(row);
		}
	}

	public static long GetClasses(byte classJobCategoryId)
	{
		if (cache == null || classJobCategoryId >= cache.Length)
		{
			return 0;
		}

		return cache[classJobCategoryId];
	}

	private static long ToFlags(ClassJobCategory row)
	{
		long flags = 0;
		if (row.GLA)
		{
			flags |= 1L << 18;
		}

		if (row.PGL)
		{
			flags |= 1L << 29;
		}

		if (row.MRD)
		{
			flags |= 1L << 24;
		}

		if (row.LNC)
		{
			flags |= 1L << 21;
		}

		if (row.ARC)
		{
			flags |= 1L << 3;
		}

		if (row.CNJ)
		{
			flags |= 1L << 12;
		}

		if (row.THM)
		{
			flags |= 1L << 35;
		}

		if (row.CRP)
		{
			flags |= 1L << 11;
		}

		if (row.BSM)
		{
			flags |= 1L << 8;
		}

		if (row.ARM)
		{
			flags |= 1L << 4;
		}

		if (row.GSM)
		{
			flags |= 1L << 19;
		}

		if (row.LTW)
		{
			flags |= 1L << 22;
		}

		if (row.WVR)
		{
			flags |= 1L << 37;
		}

		if (row.ALC)
		{
			flags |= 1L << 1;
		}

		if (row.CUL)
		{
			flags |= 1L << 13;
		}

		if (row.MIN)
		{
			flags |= 1L << 25;
		}

		if (row.BTN)
		{
			flags |= 1L << 10;
		}

		if (row.FSH)
		{
			flags |= 1L << 17;
		}

		if (row.PLD)
		{
			flags |= 1L << 28;
		}

		if (row.MNK)
		{
			flags |= 1L << 26;
		}

		if (row.WAR)
		{
			flags |= 1L << 36;
		}

		if (row.DRG)
		{
			flags |= 1L << 16;
		}

		if (row.BRD)
		{
			flags |= 1L << 6;
		}

		if (row.WHM)
		{
			flags |= 1L << 38;
		}

		if (row.BLM)
		{
			flags |= 1L << 7;
		}

		if (row.ACN)
		{
			flags |= 1L << 2;
		}

		if (row.SMN)
		{
			flags |= 1L << 34;
		}

		if (row.SCH)
		{
			flags |= 1L << 33;
		}

		if (row.ROG)
		{
			flags |= 1L << 31;
		}

		if (row.NIN)
		{
			flags |= 1L << 27;
		}

		if (row.MCH)
		{
			flags |= 1L << 23;
		}

		if (row.DRK)
		{
			flags |= 1L << 15;
		}

		if (row.AST)
		{
			flags |= 1L << 5;
		}

		if (row.SAM)
		{
			flags |= 1L << 32;
		}

		if (row.RDM)
		{
			flags |= 1L << 30;
		}

		if (row.BLU)
		{
			flags |= 1L << 9;
		}

		if (row.GNB)
		{
			flags |= 1L << 20;
		}

		if (row.DNC)
		{
			flags |= 1L << 14;
		}

		if (row.RPR)
		{
			flags |= 1L << 39;
		}

		if (row.SGE)
		{
			flags |= 1L << 40;
		}

		if (row.VPR)
		{
			flags |= 1L << 41;
		}

		if (row.PCT)
		{
			flags |= 1L << 42;
		}

		return flags;
	}
}
