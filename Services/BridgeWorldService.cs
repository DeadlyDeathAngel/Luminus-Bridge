// © DeadlyDeathAngel.
// Licensed under the MIT license.

namespace LuminusBridge.Services;

using Dalamud;
using Dalamud.Plugin.Services;
using System;

/// <summary>
/// Eorzea time and weather read/write (matches desktop <see cref="Luminus.TimeService"/> /
/// <see cref="Luminus.TerritoryService"/> memory layout).
/// Time freeze uses a TimeAsm NOP patch (desktop <see cref="Luminus.TimeService"/>)
/// plus per-frame Eorzea time writes while frozen.
/// </summary>
public sealed unsafe class BridgeWorldService : IDisposable
{
	private const string FrameworkInstanceSignature = "48 8B 1D ?? ?? ?? ?? 8B 7C 24";
	private const string GposeFiltersSignature = "48 85 D2 4C 8B 05 ?? ?? ?? ??";
	private const string WeatherManagerSignature = "48 8D 0D ?? ?? ?? ?? 44 0F B7 45";
	private const string TimeAsmSignature = "48 89 87 ?? ?? ?? ?? 48 69 C0";
	private const int TimeAsmNopCount = 7;

	private const int OffsetEorzeaTime = 0x1778;
	private const int OffsetOverrideEorzeaTime = 0x17A0;
	private const int OffsetIsTimeOverridden = 0x17A8;
	private const int ServerWeatherFromManagerOffset = 0x48;
	private const int NextWeatherIdOffset = 0x08;
	private const int GposeWeatherOffset = 0x27;
	private const long EorzeaMonthCycleSeconds = 2764800;

	private readonly ISigScanner sigScanner;
	private readonly IPluginLog log;
	private readonly IClientState clientState;
	private readonly BridgeGameDataService gameDataService;
	private readonly object gate = new();

	private nint frameworkStatic;
	private nint gposeFiltersStatic;
	private nint serverWeatherPtr;
	private bool addressesResolved;
	private bool addressesAvailable;

	private nint timeAsmAddress;
	private byte[]? timeAsmOriginal;
	private bool timeAsmPatchActive;
	private bool timeAsmResolveAttempted;

	private TimeHoldState? timeHold;
	private WeatherHoldState? weatherHold;

	public BridgeWorldService(
		ISigScanner sigScanner,
		IPluginLog log,
		IClientState clientState,
		BridgeGameDataService gameDataService)
	{
		this.sigScanner = sigScanner;
		this.log = log;
		this.clientState = clientState;
		this.gameDataService = gameDataService;
	}

	public void Dispose()
	{
		this.SetTimeAsmFrozen(false);
	}

	public void OnFrameworkTick(bool isInGpose)
	{
		if (!this.clientState.IsLoggedIn)
		{
			this.SetTimeAsmFrozen(false);
			lock (this.gate)
			{
				this.timeHold = null;
				this.weatherHold = null;
			}

			return;
		}

		TimeHoldState? timeHold;
		WeatherHoldState? weatherHold;
		lock (this.gate)
		{
			timeHold = this.timeHold;
			weatherHold = this.weatherHold;
		}

		if (timeHold?.FreezeTime == true && this.TryGetFramework(out nint framework, out _))
		{
			this.WriteTime(framework, timeHold.TimeOfDayMinutes, timeHold.DayOfMonth);
		}

		if (weatherHold?.Active == true)
		{
			this.TryWriteWeather(isInGpose, weatherHold.WeatherId, out _);
		}
	}

	public WorldSnapshot Read(bool isInGpose)
	{
		if (!this.clientState.IsLoggedIn)
		{
			return WorldSnapshot.Unavailable("Not signed in.");
		}

		if (!this.TryGetFramework(out nint framework, out string? frameworkError))
		{
			return WorldSnapshot.Unavailable(frameworkError ?? "Framework unavailable.");
		}

		long timeOfDayMinutes;
		byte dayOfMonth;
		string timeString;
		bool freezeTime;
		lock (this.gate)
		{
			freezeTime = this.timeHold?.FreezeTime ?? false;
			if (freezeTime && this.timeHold != null)
			{
				timeOfDayMinutes = this.timeHold.TimeOfDayMinutes;
				dayOfMonth = this.timeHold.DayOfMonth;
				timeString = FormatTimeString(timeOfDayMinutes);
			}
			else
			{
				this.ReadTime(framework, out timeOfDayMinutes, out dayOfMonth, out timeString);
			}
		}

		bool holdWeather;
		ushort heldWeatherId = 0;
		lock (this.gate)
		{
			if (this.weatherHold?.Active == true)
			{
				holdWeather = true;
				heldWeatherId = this.weatherHold.WeatherId;
			}
			else
			{
				holdWeather = false;
			}
		}

		ushort weatherId;
		if (holdWeather)
		{
			weatherId = heldWeatherId;
		}
		else if (!this.TryReadWeather(isInGpose, out weatherId, out _))
		{
			weatherId = 0;
		}

		this.gameDataService.TryGetWeather(weatherId, out string weatherName, out uint weatherIconId);

		return new WorldSnapshot(
			Available: true,
			IsInGpose: isInGpose,
			TimeOfDayMinutes: timeOfDayMinutes,
			TimeString: timeString,
			DayOfMonth: dayOfMonth,
			WeatherId: weatherId,
			WeatherName: weatherName,
			WeatherIconId: weatherIconId,
			FreezeTime: freezeTime,
			HoldWeather: holdWeather,
			Error: null);
	}

	public (bool Ok, string? Error) Apply(bool isInGpose, WorldUpdate update)
	{
		if (!this.clientState.IsLoggedIn)
		{
			return (false, "Not signed in.");
		}

		if (!this.TryGetFramework(out nint framework, out string? frameworkError))
		{
			return (false, frameworkError ?? "Framework unavailable.");
		}

		long? timeOfDayMinutes = update.TimeOfDayMinutes;
		byte? dayOfMonth = update.DayOfMonth;

		if (timeOfDayMinutes.HasValue || dayOfMonth.HasValue)
		{
			this.ReadTime(framework, out long currentMinutes, out byte currentDay, out _);
			long minutes = timeOfDayMinutes ?? currentMinutes;
			byte day = dayOfMonth ?? currentDay;

			if (minutes < 0 || minutes > 1439)
			{
				return (false, "timeOfDayMinutes must be 0–1439.");
			}

			if (day < 1 || day > 32)
			{
				return (false, "dayOfMonth must be 1–32.");
			}

			this.WriteTime(framework, minutes, day);

			lock (this.gate)
			{
				this.timeHold ??= new TimeHoldState();
				this.timeHold.TimeOfDayMinutes = minutes;
				this.timeHold.DayOfMonth = day;
			}

			bool frozen;
			lock (this.gate)
			{
				frozen = this.timeHold?.FreezeTime ?? false;
			}

			if (frozen)
			{
				this.SetTimeAsmFrozen(true);
				this.WriteTime(framework, minutes, day);
			}
		}

		if (update.FreezeTime.HasValue)
		{
			lock (this.gate)
			{
				this.timeHold ??= new TimeHoldState();
				this.timeHold.FreezeTime = update.FreezeTime.Value;

				if (this.timeHold.FreezeTime && !timeOfDayMinutes.HasValue && !dayOfMonth.HasValue)
				{
					this.ReadTime(framework, out long currentMinutes, out byte currentDay, out _);
					this.timeHold.TimeOfDayMinutes = currentMinutes;
					this.timeHold.DayOfMonth = currentDay;
				}
			}

			this.SetTimeAsmFrozen(update.FreezeTime.Value);

			if (update.FreezeTime.Value)
			{
				TimeHoldState hold;
				lock (this.gate)
				{
					hold = this.timeHold!;
				}

				this.WriteTime(framework, hold.TimeOfDayMinutes, hold.DayOfMonth);
			}
		}

		if (update.HoldWeather == false)
		{
			lock (this.gate)
			{
				this.weatherHold = null;
			}
		}

		if (update.WeatherId.HasValue)
		{
			ushort weatherId = update.WeatherId.Value;
			if (!this.TryWriteWeather(isInGpose, weatherId, out string? weatherError))
			{
				return (false, weatherError);
			}

			bool hold = update.HoldWeather ?? true;
			lock (this.gate)
			{
				if (hold)
				{
					this.weatherHold = new WeatherHoldState { Active = true, WeatherId = weatherId };
				}
				else
				{
					this.weatherHold = null;
				}
			}
		}

		return (true, null);
	}

	private void ReadTime(nint framework, out long timeOfDayMinutes, out byte dayOfMonth, out string timeString)
	{
		long currentTime = this.ReadCurrentEorzeaTime(framework);
		long timeVal = currentTime % EorzeaMonthCycleSeconds;
		long secondInDay = timeVal % 86400;
		timeOfDayMinutes = secondInDay / 60;
		dayOfMonth = (byte)((timeVal / 86400) + 1);
		timeString = FormatTimeString(timeOfDayMinutes);
	}

	private static string FormatTimeString(long timeOfDayMinutes)
	{
		var displayTime = TimeSpan.FromMinutes(timeOfDayMinutes);
		return string.Create(5, displayTime, static (span, value) =>
		{
			span[0] = (char)('0' + (value.Hours / 10));
			span[1] = (char)('0' + (value.Hours % 10));
			span[2] = ':';
			span[3] = (char)('0' + (value.Minutes / 10));
			span[4] = (char)('0' + (value.Minutes % 10));
		});
	}

	private void WriteTime(nint framework, long timeOfDayMinutes, byte dayOfMonth)
	{
		long currentTime = this.ReadCurrentEorzeaTime(framework);
		long monthBase = currentTime - (currentTime % EorzeaMonthCycleSeconds);
		long newTimeVal = (timeOfDayMinutes * 60) + (86400 * (dayOfMonth - 1));
		long newTime = monthBase + newTimeVal;
		*(long*)(framework + OffsetEorzeaTime) = newTime;

		if (*(bool*)(framework + OffsetIsTimeOverridden))
		{
			*(long*)(framework + OffsetOverrideEorzeaTime) = newTime;
		}
	}

	private long ReadCurrentEorzeaTime(nint framework)
	{
		bool overridden = *(bool*)(framework + OffsetIsTimeOverridden);
		return overridden
			? *(long*)(framework + OffsetOverrideEorzeaTime)
			: *(long*)(framework + OffsetEorzeaTime);
	}

	private bool TryReadWeather(bool isInGpose, out ushort weatherId, out string? error)
	{
		weatherId = 0;
		error = null;

		if (!this.EnsureAddresses())
		{
			error = "World memory addresses unavailable.";
			return false;
		}

		if (isInGpose)
		{
			nint filters = *(nint*)this.gposeFiltersStatic;
			if (filters == 0)
			{
				error = "GPose filters unavailable.";
				return false;
			}

			weatherId = *(ushort*)(filters + GposeWeatherOffset);
			return true;
		}

		if (this.serverWeatherPtr == 0)
		{
			error = "Server weather unavailable.";
			return false;
		}

		weatherId = *(byte*)(this.serverWeatherPtr + NextWeatherIdOffset);
		return true;
	}

	private bool TryWriteWeather(bool isInGpose, ushort weatherId, out string? error)
	{
		error = null;

		if (!this.EnsureAddresses())
		{
			error = "World memory addresses unavailable.";
			return false;
		}

		if (isInGpose)
		{
			nint filters = *(nint*)this.gposeFiltersStatic;
			if (filters == 0)
			{
				error = "GPose filters unavailable.";
				return false;
			}

			*(ushort*)(filters + GposeWeatherOffset) = weatherId;
			return true;
		}

		if (this.serverWeatherPtr == 0)
		{
			error = "Server weather unavailable.";
			return false;
		}

		*(byte*)(this.serverWeatherPtr + NextWeatherIdOffset) = (byte)weatherId;
		return true;
	}

	private bool TryGetFramework(out nint framework, out string? error)
	{
		framework = 0;
		error = null;

		if (!this.EnsureAddresses())
		{
			error = "Framework unavailable.";
			return false;
		}

		framework = *(nint*)this.frameworkStatic;
		if (framework == 0)
		{
			error = "Framework instance is null.";
			return false;
		}

		return true;
	}

	private bool EnsureAddresses()
	{
		if (this.addressesResolved)
		{
			return this.addressesAvailable;
		}

		lock (this.gate)
		{
			if (this.addressesResolved)
			{
				return this.addressesAvailable;
			}

			try
			{
				this.frameworkStatic = this.sigScanner.GetStaticAddressFromSig(FrameworkInstanceSignature);
				this.gposeFiltersStatic = this.sigScanner.GetStaticAddressFromSig(GposeFiltersSignature);
				nint weatherManager = this.sigScanner.GetStaticAddressFromSig(WeatherManagerSignature, 3);
				this.serverWeatherPtr = weatherManager != 0 ? weatherManager + ServerWeatherFromManagerOffset : 0;

				this.addressesAvailable = this.frameworkStatic != 0 && this.gposeFiltersStatic != 0;
				if (!this.addressesAvailable)
				{
					this.log.Warning("LuminusBridge world memory signatures not fully resolved.");
				}
			}
			catch (Exception ex)
			{
				this.log.Warning(ex, "Failed to resolve LuminusBridge world memory signatures.");
				this.addressesAvailable = false;
			}

			this.addressesResolved = true;
		}

		return this.addressesAvailable;
	}

	private void EnsureTimeAsm()
	{
		if (this.timeAsmOriginal != null || this.timeAsmResolveAttempted)
		{
			return;
		}

		this.timeAsmResolveAttempted = true;

		if (!this.sigScanner.TryScanText(TimeAsmSignature, out nint address) || address == 0)
		{
			this.log.Warning("LuminusBridge could not resolve TimeAsm signature for time freeze.");
			return;
		}

		if (!SafeMemory.ReadBytes(address, TimeAsmNopCount, out byte[]? original) || original == null)
		{
			this.log.Warning("LuminusBridge could not read TimeAsm bytes for time freeze.");
			return;
		}

		this.timeAsmAddress = address;
		this.timeAsmOriginal = original;
		this.log.Information("LuminusBridge TimeAsm patch ready for time freeze.");
	}

	private void SetTimeAsmFrozen(bool frozen)
	{
		if (frozen)
		{
			this.EnsureTimeAsm();
		}

		if (this.timeAsmAddress == 0 || this.timeAsmOriginal == null)
		{
			return;
		}

		if (frozen == this.timeAsmPatchActive)
		{
			return;
		}

		if (frozen)
		{
			byte[] nops = new byte[TimeAsmNopCount];
			Array.Fill(nops, (byte)0x90);
			SafeMemory.WriteBytes(this.timeAsmAddress, nops);
		}
		else
		{
			SafeMemory.WriteBytes(this.timeAsmAddress, this.timeAsmOriginal);
		}

		this.timeAsmPatchActive = frozen;
	}

	private sealed class TimeHoldState
	{
		public long TimeOfDayMinutes { get; set; }

		public byte DayOfMonth { get; set; } = 1;

		public bool FreezeTime { get; set; }
	}

	private sealed class WeatherHoldState
	{
		public bool Active { get; set; }

		public ushort WeatherId { get; set; }
	}
}

public readonly record struct WorldSnapshot(
	bool Available,
	bool IsInGpose,
	long TimeOfDayMinutes,
	string TimeString,
	byte DayOfMonth,
	ushort WeatherId,
	string WeatherName,
	uint WeatherIconId,
	bool FreezeTime,
	bool HoldWeather,
	string? Error)
{
	public static WorldSnapshot Unavailable(string error)
		=> new(false, false, 0, "00:00", 1, 0, string.Empty, 0, false, false, error);
}

public readonly record struct WorldUpdate(
	long? TimeOfDayMinutes,
	byte? DayOfMonth,
	ushort? WeatherId,
	bool? FreezeTime,
	bool? HoldWeather);
