// © DeadlyDeathAngel.
// Licensed under the MIT license.

namespace AnamnesisBridge.Services;

using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using System;
using System.Numerics;

/// <summary>
/// GPose camera read/write using the same field layout as desktop Anamnesis
/// (<see cref="Anamnesis.Memory.CameraMemory"/> / GPose target position).
/// Values are held and re-applied after each camera update so the game cannot snap back.
/// </summary>
public sealed unsafe class BridgeCameraService : IDisposable
{
	private const string CameraManagerSignature = "48 8D 35 ?? ?? ?? ?? 48 8B 09";
	private const string TargetSystemSignature = "48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 3B C6 0F 95 C0";
	private const string CameraUpdateSignature =
		"40 55 53 57 48 8D 6C 24 A0 48 81 EC ?? ?? ?? ?? 48 8B 1D";

	private const int GposeTargetOffset = 0x98;
	private const int GposeTargetPositionOffset = 0xB0;

	private const int OffsetZoom = 0x124;
	private const int OffsetMinZoom = 0x128;
	private const int OffsetMaxZoom = 0x12C;
	private const int OffsetFieldOfView = 0x13C;
	private const int OffsetAngle = 0x140;
	private const int OffsetYMin = 0x158;
	private const int OffsetYMax = 0x15C;
	private const int OffsetPan = 0x160;
	private const int OffsetRotation = 0x170;

	private readonly ISigScanner sigScanner;
	private readonly IGameInteropProvider interopProvider;
	private readonly IPluginLog log;
	private readonly IClientState clientState;
	private readonly ActorSkeletonService skeletonService;
	private readonly object gate = new();

	private nint cameraManagerStatic;
	private nint targetSystemStatic;
	private bool addressesResolved;
	private bool addressesAvailable;

	private CameraHoldState? hold;
	private Hook<CameraUpdateDelegate>? cameraUpdateHook;
	private bool cameraUpdateHookInitialized;

	public BridgeCameraService(
		ISigScanner sigScanner,
		IGameInteropProvider interopProvider,
		IPluginLog log,
		IClientState clientState,
		ActorSkeletonService skeletonService)
	{
		this.sigScanner = sigScanner;
		this.interopProvider = interopProvider;
		this.log = log;
		this.clientState = clientState;
		this.skeletonService = skeletonService;
	}

	public void Dispose()
	{
		this.cameraUpdateHook?.Dispose();
	}

	public void OnFrameworkTick(bool isInGpose)
	{
		if (!isInGpose)
		{
			lock (this.gate)
			{
				this.hold = null;
			}

			return;
		}

		this.ReapplyHold();
	}

	public CameraSnapshot Read()
	{
		if (!this.clientState.IsGPosing)
		{
			return CameraSnapshot.Unavailable("Not in GPose.");
		}

		if (!this.TryGetCamera(out nint camera, out string? cameraError))
		{
			return CameraSnapshot.Unavailable(cameraError ?? "Camera unavailable.");
		}

		return this.ReadSnapshot(camera);
	}

	public (bool Ok, string? Error) Apply(CameraUpdate update)
	{
		if (!this.clientState.IsGPosing)
		{
			return (false, "Not in GPose.");
		}

		if (!this.TryGetCamera(out nint camera, out string? cameraError))
		{
			return (false, cameraError ?? "Camera unavailable.");
		}

		if (update.Zoom is float zoom && update.DelimitCamera == null && !IsDelimitCamera(
				ReadFloat(camera, OffsetMinZoom),
				ReadFloat(camera, OffsetMaxZoom))
			&& (zoom > 20f || zoom < 1.75f))
		{
			ApplyDelimit(camera, delimit: true);
		}

		if (update.DelimitCamera is bool delimit)
		{
			ApplyDelimit(camera, delimit);
		}

		if (update.Zoom is float zoomValue)
		{
			WriteFloat(camera, OffsetZoom, zoomValue);
		}

		if (update.FieldOfView is float fieldOfView)
		{
			WriteFloat(camera, OffsetFieldOfView, fieldOfView);
		}

		if (update.AngleXDeg is float angleX || update.AngleYDeg is float angleY)
		{
			Vector2 angle = ReadVector2(camera, OffsetAngle);
			if (update.AngleXDeg is float ax)
			{
				angle.X = DegToRad(ax);
			}

			if (update.AngleYDeg is float ay)
			{
				angle.Y = DegToRad(ay);
			}

			WriteVector2(camera, OffsetAngle, angle);
		}

		if (update.RotationDeg is float rotationDeg)
		{
			WriteFloat(camera, OffsetRotation, DegToRad(rotationDeg));
		}

		if (update.PanXDeg is float panX || update.PanYDeg is float panY)
		{
			Vector2 pan = ReadVector2(camera, OffsetPan);
			if (update.PanXDeg is float px)
			{
				pan.X = DegToRad(px);
			}

			if (update.PanYDeg is float py)
			{
				pan.Y = DegToRad(py);
			}

			WriteVector2(camera, OffsetPan, pan);
		}

		nint gposeTarget = 0;
		if (update.PositionX != null || update.PositionY != null || update.PositionZ != null)
		{
			if (!this.TryGetGposeTarget(out gposeTarget))
			{
				return (false, "GPose camera target unavailable.");
			}

			Vector3 position = ReadVector3(gposeTarget, GposeTargetPositionOffset);
			if (update.PositionX is float x)
			{
				position.X = x;
			}

			if (update.PositionY is float y)
			{
				position.Y = y;
			}

			if (update.PositionZ is float z)
			{
				position.Z = z;
			}

			WriteVector3(gposeTarget, GposeTargetPositionOffset, position);
		}

		CameraSnapshot snapshot = this.ReadSnapshot(camera);
		lock (this.gate)
		{
			this.hold = CameraHoldState.FromSnapshot(snapshot);
			this.EnsureCameraUpdateHook();
		}

		this.ReapplyHold();
		return (true, null);
	}

	public (bool Ok, CameraShotData? Shot, string? Error) ExportShot(int objectIndex)
	{
		if (!this.clientState.IsGPosing)
		{
			return (false, null, "Not in GPose.");
		}

		if (!this.skeletonService.TryGetModelTransform(objectIndex, out ActorModelTransformSnapshot actor, out string? actorError))
		{
			return (false, null, actorError ?? "Actor transform unavailable.");
		}

		if (!this.TryGetCamera(out nint camera, out string? cameraError))
		{
			return (false, null, cameraError ?? "Camera unavailable.");
		}

		CameraSnapshot snapshot = this.ReadSnapshot(camera);
		return (true, BridgeCameraShotMath.ExportShot(snapshot, actor), null);
	}

	public (bool Ok, string? Error) ApplyShot(int objectIndex, CameraShotData shot)
	{
		if (!this.skeletonService.TryGetModelTransform(objectIndex, out ActorModelTransformSnapshot actor, out string? actorError))
		{
			return (false, actorError ?? "Actor transform unavailable.");
		}

		BridgeCameraShotMath.ApplyShot(shot, actor, out CameraUpdate update, out _);
		return this.Apply(update);
	}

	private CameraSnapshot ReadSnapshot(nint camera)
	{
		Vector2 angle = ReadVector2(camera, OffsetAngle);
		Vector2 pan = ReadVector2(camera, OffsetPan);
		Vector3 position = default;
		if (this.TryGetGposeTarget(out nint gposeTarget))
		{
			position = ReadVector3(gposeTarget, GposeTargetPositionOffset);
		}

		float minZoom = ReadFloat(camera, OffsetMinZoom);
		float maxZoom = ReadFloat(camera, OffsetMaxZoom);

		return new CameraSnapshot(
			Available: true,
			IsInGpose: true,
			Error: null,
			DelimitCamera: IsDelimitCamera(minZoom, maxZoom),
			Zoom: ReadFloat(camera, OffsetZoom),
			MinZoom: minZoom,
			MaxZoom: maxZoom,
			FieldOfView: ReadFloat(camera, OffsetFieldOfView),
			AngleXDeg: RadToDeg(angle.X),
			AngleYDeg: RadToDeg(angle.Y),
			RotationDeg: RadToDeg(ReadFloat(camera, OffsetRotation)),
			PanXDeg: RadToDeg(pan.X),
			PanYDeg: RadToDeg(pan.Y),
			PositionX: position.X,
			PositionY: position.Y,
			PositionZ: position.Z);
	}

	private void ReapplyHold()
	{
		CameraHoldState? snapshot;
		lock (this.gate)
		{
			snapshot = this.hold;
		}

		if (snapshot == null || !this.clientState.IsGPosing)
		{
			return;
		}

		if (!this.TryGetCamera(out nint camera, out _))
		{
			return;
		}

		snapshot.WriteTo(camera, this);
	}

	private void EnsureCameraUpdateHook()
	{
		if (this.cameraUpdateHookInitialized)
		{
			return;
		}

		try
		{
			nint address = this.sigScanner.ScanText(CameraUpdateSignature);
			if (address == nint.Zero)
			{
				this.log.Warning("AnamnesisBridge camera update hook signature not found.");
				return;
			}

			this.cameraUpdateHook = this.interopProvider.HookFromAddress<CameraUpdateDelegate>(
				address,
				this.CameraUpdateDetour);
			this.cameraUpdateHook.Enable();
			this.cameraUpdateHookInitialized = true;
			this.log.Information("AnamnesisBridge camera hold hook active.");
		}
		catch (Exception ex)
		{
			this.log.Warning(ex, "Failed to create AnamnesisBridge camera hold hook.");
		}
	}

	private nint CameraUpdateDetour(nint camera)
	{
		nint result = this.cameraUpdateHook!.Original(camera);

		if (!this.clientState.IsGPosing || camera == 0)
		{
			return result;
		}

		CameraHoldState? snapshot;
		lock (this.gate)
		{
			snapshot = this.hold;
		}

		if (snapshot != null)
		{
			snapshot.WriteTo(camera, this);
		}

		return result;
	}

	private bool TryGetCamera(out nint camera, out string? error)
	{
		camera = 0;
		error = null;

		CameraManager* manager = CameraManager.Instance();
		if (manager != null)
		{
			nint active = (nint)manager->GetActiveCamera();
			if (active != 0)
			{
				camera = active;
				return true;
			}
		}

		if (!this.EnsureAddresses(out error))
		{
			return false;
		}

		camera = *(nint*)this.cameraManagerStatic;
		if (camera == 0)
		{
			error = "Camera pointer is null.";
			return false;
		}

		return true;
	}

	private bool TryGetGposeTarget(out nint gposeTarget)
	{
		gposeTarget = 0;
		if (!this.EnsureAddresses(out _))
		{
			return false;
		}

		gposeTarget = *(nint*)(this.targetSystemStatic + GposeTargetOffset);
		return gposeTarget != 0;
	}

	private bool EnsureAddresses(out string? error)
	{
		if (!this.addressesResolved)
		{
			try
			{
				this.cameraManagerStatic = this.sigScanner.GetStaticAddressFromSig(CameraManagerSignature);
				this.targetSystemStatic = this.sigScanner.GetStaticAddressFromSig(TargetSystemSignature);
				this.addressesAvailable = this.cameraManagerStatic != 0 && this.targetSystemStatic != 0;
			}
			catch (Exception ex)
			{
				this.addressesAvailable = false;
				error = $"Camera address scan failed: {ex.Message}";
				this.addressesResolved = true;
				return false;
			}

			this.addressesResolved = true;
		}

		if (!this.addressesAvailable)
		{
			error = "Camera address scan unavailable.";
			return false;
		}

		error = null;
		return true;
	}

	private static bool IsDelimitCamera(float minZoom, float maxZoom)
		=> minZoom < 1.0f || maxZoom > 50f;

	private static void ApplyDelimit(nint camera, bool delimit)
	{
		if (delimit)
		{
			WriteFloat(camera, OffsetMinZoom, 0f);
			WriteFloat(camera, OffsetMaxZoom, 350f);
			WriteFloat(camera, OffsetYMin, -1.5f);
			WriteFloat(camera, OffsetYMax, 1.5f);
		}
		else
		{
			WriteFloat(camera, OffsetMinZoom, 1.75f);
			WriteFloat(camera, OffsetMaxZoom, 20f);
			WriteFloat(camera, OffsetYMin, -1.4f);
			WriteFloat(camera, OffsetYMax, 1.25f);
		}
	}

	private static float ReadFloat(nint address, int offset)
		=> *(float*)(address + offset);

	private static void WriteFloat(nint address, int offset, float value)
		=> *(float*)(address + offset) = value;

	private static Vector2 ReadVector2(nint address, int offset)
		=> *(Vector2*)(address + offset);

	private static void WriteVector2(nint address, int offset, Vector2 value)
		=> *(Vector2*)(address + offset) = value;

	private static Vector3 ReadVector3(nint address, int offset)
		=> *(Vector3*)(address + offset);

	private static void WriteVector3(nint address, int offset, Vector3 value)
		=> *(Vector3*)(address + offset) = value;

	private static float RadToDeg(float radians)
		=> radians * (180f / MathF.PI);

	private static float DegToRad(float degrees)
		=> degrees * (MathF.PI / 180f);

	private delegate nint CameraUpdateDelegate(nint camera);

	private sealed class CameraHoldState
	{
		public required bool DelimitCamera { get; init; }

		public required float Zoom { get; init; }

		public required float FieldOfView { get; init; }

		public required float AngleXRad { get; init; }

		public required float AngleYRad { get; init; }

		public required float RotationRad { get; init; }

		public required float PanXRad { get; init; }

		public required float PanYRad { get; init; }

		public required Vector3 Position { get; init; }

		public static CameraHoldState FromSnapshot(CameraSnapshot snapshot)
			=> new()
			{
				DelimitCamera = snapshot.DelimitCamera,
				Zoom = snapshot.Zoom,
				FieldOfView = snapshot.FieldOfView,
				AngleXRad = DegToRad(snapshot.AngleXDeg),
				AngleYRad = DegToRad(snapshot.AngleYDeg),
				RotationRad = DegToRad(snapshot.RotationDeg),
				PanXRad = DegToRad(snapshot.PanXDeg),
				PanYRad = DegToRad(snapshot.PanYDeg),
				Position = new Vector3(snapshot.PositionX, snapshot.PositionY, snapshot.PositionZ),
			};

		public void WriteTo(nint camera, BridgeCameraService service)
		{
			ApplyDelimit(camera, this.DelimitCamera);
			WriteFloat(camera, OffsetZoom, this.Zoom);
			WriteFloat(camera, OffsetFieldOfView, this.FieldOfView);
			WriteVector2(camera, OffsetAngle, new Vector2(this.AngleXRad, this.AngleYRad));
			WriteFloat(camera, OffsetRotation, this.RotationRad);
			WriteVector2(camera, OffsetPan, new Vector2(this.PanXRad, this.PanYRad));

			if (service.TryGetGposeTarget(out nint gposeTarget))
			{
				WriteVector3(gposeTarget, GposeTargetPositionOffset, this.Position);
			}
		}
	}
}

public readonly record struct CameraSnapshot(
	bool Available,
	bool IsInGpose,
	string? Error,
	bool DelimitCamera,
	float Zoom,
	float MinZoom,
	float MaxZoom,
	float FieldOfView,
	float AngleXDeg,
	float AngleYDeg,
	float RotationDeg,
	float PanXDeg,
	float PanYDeg,
	float PositionX,
	float PositionY,
	float PositionZ)
{
	public static CameraSnapshot Unavailable(string error)
		=> new(
			Available: false,
			IsInGpose: false,
			Error: error,
			DelimitCamera: false,
			Zoom: 0,
			MinZoom: 0,
			MaxZoom: 0,
			FieldOfView: 0,
			AngleXDeg: 0,
			AngleYDeg: 0,
			RotationDeg: 0,
			PanXDeg: 0,
			PanYDeg: 0,
			PositionX: 0,
			PositionY: 0,
			PositionZ: 0);
}

public readonly record struct CameraUpdate(
	bool? DelimitCamera = null,
	float? Zoom = null,
	float? FieldOfView = null,
	float? AngleXDeg = null,
	float? AngleYDeg = null,
	float? RotationDeg = null,
	float? PanXDeg = null,
	float? PanYDeg = null,
	float? PositionX = null,
	float? PositionY = null,
	float? PositionZ = null);
