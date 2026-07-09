// © DeadlyDeathAngel.
// Licensed under the MIT license.

namespace LuminusBridge.Services;

using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.Havok.Animation.Rig;
using FFXIVClientStructs.Havok.Common.Base.Math.QsTransform;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

/// <summary>
/// In-process Havok/game-object hooks for GPose posing and freeze (0.2.7.0).
/// Hooks install lazily on the framework thread when a flag is first enabled.
/// </summary>
public sealed class BridgePosingHooks : IDisposable
{
	private const string SigSetBoneModelTransform =
		"48 8B C4 48 89 58 18 55 56 57 41 54 41 55 41 56 41 57 48 81 EC ?? ?? ?? ?? 0F 29 70 B8 0F 29 78 A8 44 0F 29 40 ?? 44 0F 29 48 ?? 48 8B 05 ?? ?? ?? ??";

	private const string SigSyncModelSpace =
		"48 83 EC 18 80 79 38 00";

	private const string SigLookAtIkSolve =
		"E8 ?? ?? ?? ?? 80 7C 24 ?? ?? 48 8D 4C 24 ??";

	private const string SigApplyKineDriverTransforms =
		"48 8B C4 55 57 48 83 EC 58";

	private readonly ISigScanner sigScanner;
	private readonly IGameInteropProvider interopProvider;
	private readonly IPluginLog log;
	private readonly Func<bool> getIsInGpose;
	private readonly Func<bool> getGposeHooksAllowed;
	private readonly object gate = new();

	private Hook<SetBoneModelTransformDelegate>? hookPhysics;
	private Hook<SyncModelSpaceDelegate>? hookSyncModel;
	private Hook<CalculateBoneModelSpaceDelegate>? hookCalculateBone;
	private Hook<SetPositionDelegate>? hookSetPosition;
	private Hook<LookAtIkSolveDelegate>? hookLookAt;
	private Hook<ApplyKineDriverTransformsDelegate>? hookKineDriver;

	private bool physicsHookInitialized;
	private bool syncHookInitialized;
	private bool calculateHookInitialized;
	private bool worldHookInitialized;
	private bool lookAtHookInitialized;
	private bool kineDriverHookInitialized;
	private bool posingEnabled;
	private bool freezePhysics;
	private bool freezeWorldVisualState;

	public BridgePosingHooks(
		ISigScanner sigScanner,
		IGameInteropProvider interopProvider,
		IPluginLog log,
		Func<bool> getIsInGpose,
		Func<bool> getGposeHooksAllowed)
	{
		this.sigScanner = sigScanner;
		this.interopProvider = interopProvider;
		this.log = log;
		this.getIsInGpose = getIsInGpose;
		this.getGposeHooksAllowed = getGposeHooksAllowed;
	}

	public bool HooksActive
	{
		get
		{
			lock (this.gate)
			{
				return this.physicsHookInitialized || this.syncHookInitialized || this.calculateHookInitialized || this.worldHookInitialized || this.lookAtHookInitialized || this.kineDriverHookInitialized;
			}
		}
	}

	/// <summary>Must run on the Dalamud framework thread (IPC POST path).</summary>
	public void ApplyState(bool posingEnabled, bool freezePhysics, bool freezeWorldVisualState)
	{
		lock (this.gate)
		{
			this.posingEnabled = posingEnabled;
			this.freezePhysics = freezePhysics;
			this.freezeWorldVisualState = freezeWorldVisualState;

			if (freezePhysics || posingEnabled)
			{
				this.EnsurePhysicsHook();
			}

			if (posingEnabled)
			{
				this.EnsureSyncModelHook();
				this.EnsureCalculateBoneHook();
				this.EnsureLookAtHook();
				this.EnsureKineDriverHook();
			}

			if (freezeWorldVisualState)
			{
				this.EnsureWorldHooks();
			}
		}
	}

	public void DisableAll()
	{
		lock (this.gate)
		{
			this.posingEnabled = false;
			this.freezePhysics = false;
			this.freezeWorldVisualState = false;
		}
	}

	public void Dispose()
	{
		this.hookPhysics?.Dispose();
		this.hookSyncModel?.Dispose();
		this.hookCalculateBone?.Dispose();
		this.hookSetPosition?.Dispose();
		this.hookLookAt?.Dispose();
		this.hookKineDriver?.Dispose();
	}

	private void EnsurePhysicsHook()
	{
		if (this.physicsHookInitialized)
		{
			return;
		}

		if (!this.TryHookFromSignature(
			SigSetBoneModelTransform,
			this.DetourSetBoneModelTransform,
			out this.hookPhysics))
		{
			return;
		}

		this.physicsHookInitialized = true;
		this.log.Information("LuminusBridge physics freeze hook active.");
	}

	private void EnsureSyncModelHook()
	{
		if (this.syncHookInitialized)
		{
			return;
		}

		if (!this.TryHookFromSignature(
			SigSyncModelSpace,
			this.DetourSyncModelSpace,
			out this.hookSyncModel))
		{
			return;
		}

		this.syncHookInitialized = true;
		this.log.Information("LuminusBridge pose sync hook active.");
	}

	private unsafe void EnsureCalculateBoneHook()
	{
		if (this.calculateHookInitialized)
		{
			return;
		}

		nint address = this.ResolveCalculateBoneModelSpaceAddress();
		if (address == nint.Zero)
		{
			this.log.Warning("LuminusBridge could not resolve hkaPose.CalculateBoneModelSpace.");
			return;
		}

		try
		{
			this.hookCalculateBone = this.interopProvider.HookFromAddress<CalculateBoneModelSpaceDelegate>(
				address,
				this.DetourCalculateBoneModelSpace);
			this.hookCalculateBone.Enable();
			this.calculateHookInitialized = true;
			this.log.Information("LuminusBridge calculate bone model-space hook active.");
		}
		catch (Exception ex)
		{
			this.log.Warning(ex, "Failed to create LuminusBridge calculate bone hook.");
		}
	}

	private unsafe void EnsureLookAtHook()
	{
		if (this.lookAtHookInitialized)
		{
			return;
		}

		if (!this.TryHookFromSignature(
			SigLookAtIkSolve,
			this.DetourLookAtSolve,
			out this.hookLookAt))
		{
			return;
		}

		this.lookAtHookInitialized = true;
		this.log.Information("LuminusBridge look-at IK hook active.");
	}

	private void EnsureKineDriverHook()
	{
		if (this.kineDriverHookInitialized)
		{
			return;
		}

		if (!this.TryHookFromSignature(
			SigApplyKineDriverTransforms,
			this.DetourApplyKineDriverTransforms,
			out this.hookKineDriver))
		{
			return;
		}

		this.kineDriverHookInitialized = true;
		this.log.Information("LuminusBridge kine driver hook active.");
	}

	private void EnsureWorldHooks()
	{
		if (this.worldHookInitialized)
		{
			return;
		}

		nint address = this.ResolveSetPositionAddress();
		if (address == nint.Zero)
		{
			this.log.Warning("LuminusBridge could not resolve GameObject.SetPosition for world freeze.");
			return;
		}

		try
		{
			this.hookSetPosition = this.interopProvider.HookFromAddress<SetPositionDelegate>(
				address,
				this.DetourSetPosition);
			this.hookSetPosition.Enable();
			this.worldHookInitialized = true;
			this.log.Information("LuminusBridge world freeze hook active.");
		}
		catch (Exception ex)
		{
			this.log.Warning(ex, "Failed to create LuminusBridge world freeze hook.");
		}
	}

	private bool TryHookFromSignature<T>(string signature, T detour, out Hook<T>? hook)
		where T : Delegate
	{
		hook = null;
		try
		{
			nint address = this.sigScanner.ScanText(signature);
			if (address == nint.Zero)
			{
				this.log.Warning($"Posing hook signature not found: {signature}");
				return false;
			}

			hook = this.interopProvider.HookFromAddress(address, detour);
			hook.Enable();
			return true;
		}
		catch (Exception ex)
		{
			this.log.Warning(ex, $"Optional posing hook unavailable for {signature}");
			return false;
		}
	}

	private unsafe nint ResolveSetPositionAddress()
	{
		delegate* unmanaged<GameObject*, float, float, float, void> pointer = GameObject.MemberFunctionPointers.SetPosition;
		return pointer != null ? (nint)pointer : nint.Zero;
	}

	private unsafe nint ResolveCalculateBoneModelSpaceAddress()
	{
		delegate* unmanaged<hkaPose*, int, hkQsTransformf*> pointer = hkaPose.MemberFunctionPointers.CalculateBoneModelSpace;
		return pointer != null ? (nint)pointer : nint.Zero;
	}

	private nint DetourSetBoneModelTransform(
		nint partialPtr,
		ulong boneId,
		nint transform,
		byte bUpdateSecondaryPose,
		byte bPropagate)
	{
		if (this.freezePhysics && this.getGposeHooksAllowed() && partialPtr != nint.Zero)
		{
			return partialPtr;
		}

		return this.hookPhysics!.Original(partialPtr, boneId, transform, bUpdateSecondaryPose, bPropagate);
	}

	private void DetourSyncModelSpace(nint posePtr)
	{
		if (this.posingEnabled && this.getGposeHooksAllowed() && posePtr != nint.Zero)
		{
			return;
		}

		this.hookSyncModel!.Original(posePtr);
	}

	private unsafe hkQsTransformf* DetourCalculateBoneModelSpace(hkaPose* pose, int boneIdx)
	{
		if (this.posingEnabled && this.getGposeHooksAllowed() && pose != null)
		{
			if (boneIdx >= 0 && boneIdx < pose->ModelPose.Length && boneIdx < pose->BoneFlags.Length)
			{
				this.ClearModelDirtyChain(pose, boneIdx);
				hkQsTransformf* data = (hkQsTransformf*)pose->ModelPose.Data;
				if (data != null)
				{
					return data + boneIdx;
				}
			}
		}

		return this.hookCalculateBone!.Original(pose, boneIdx);
	}

	private unsafe void ClearModelDirtyChain(hkaPose* pose, int boneIndex)
	{
		const uint modelDirty = 1u << (int)hkaPose.BoneFlag.BoneModelDirty;
		hkaSkeleton* skeleton = pose->Skeleton;
		if (skeleton == null || boneIndex < 0 || boneIndex >= pose->BoneFlags.Length)
		{
			return;
		}

		int current = boneIndex;
		while (current >= 0 && current < pose->BoneFlags.Length && current < skeleton->ParentIndices.Length)
		{
			if ((pose->BoneFlags[current] & modelDirty) == 0)
			{
				break;
			}

			pose->BoneFlags[current] &= ~modelDirty;
			current = skeleton->ParentIndices[current];
		}
	}

	private unsafe byte* DetourLookAtSolve(
		byte* a1,
		nint a2,
		nint a3,
		float a4,
		nint a5,
		nint a6)
	{
		// Block head/eye look-at IK while posing (Face Camera uses this path).
		if (this.posingEnabled && this.getGposeHooksAllowed() && a1 != null)
		{
			*a1 = 0;
			return a1;
		}

		return this.hookLookAt!.Original(a1, a2, a3, a4, a5, a6);
	}

	private void DetourApplyKineDriverTransforms(nint kineDriverPtr, nint hkaPosePtr)
	{
		// Hair/clothing physics — block while posing so manual bone writes are not overwritten.
		if (this.posingEnabled && this.getGposeHooksAllowed() && kineDriverPtr != nint.Zero && hkaPosePtr != nint.Zero)
		{
			return;
		}

		this.hookKineDriver!.Original(kineDriverPtr, hkaPosePtr);
	}

	private nint DetourSetPosition(nint goPtr, float x, float y, float z)
	{
		// Only freeze world visuals in GPose — blocking SetPosition outside GPose glitches movement.
		if (this.freezeWorldVisualState && this.getIsInGpose() && goPtr != nint.Zero)
		{
			return goPtr;
		}

		return this.hookSetPosition!.Original(goPtr, x, y, z);
	}

	[UnmanagedFunctionPointer(CallingConvention.ThisCall)]
	private unsafe delegate nint SetBoneModelTransformDelegate(
		nint partialPtr,
		ulong boneId,
		nint transform,
		byte bUpdateSecondaryPose,
		byte bPropagate);

	[UnmanagedFunctionPointer(CallingConvention.ThisCall)]
	private delegate void SyncModelSpaceDelegate(nint posePtr);

	[UnmanagedFunctionPointer(CallingConvention.ThisCall)]
	private unsafe delegate hkQsTransformf* CalculateBoneModelSpaceDelegate(hkaPose* pose, int boneIdx);

	[UnmanagedFunctionPointer(CallingConvention.ThisCall)]
	private delegate nint SetPositionDelegate(nint goPtr, float x, float y, float z);

	[UnmanagedFunctionPointer(CallingConvention.Winapi)]
	private unsafe delegate byte* LookAtIkSolveDelegate(
		byte* a1,
		nint a2,
		nint a3,
		float a4,
		nint a5,
		nint a6);

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	private delegate void ApplyKineDriverTransformsDelegate(nint kineDriverPtr, nint hkaPosePtr);
}
