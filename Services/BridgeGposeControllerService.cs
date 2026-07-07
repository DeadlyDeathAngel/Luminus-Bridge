// © DeadlyDeathAngel.
// Licensed under the MIT license.

namespace AnamnesisBridge.Services;

using FFXIVClientStructs.FFXIV.Client.Game.Event;
using System;

/// <summary>Reads GPose UI settings (Face Camera, Gaze Camera) via EventFramework.</summary>
public sealed unsafe class BridgeGposeControllerService
{
	public GposeCameraState Snapshot()
	{
		if (!this.TryGetController(out EventGPoseController* controller))
		{
			return new GposeCameraState(Available: false, FaceCameraEnabled: false, GazeCameraEnabled: false);
		}

		return new GposeCameraState(
			Available: true,
			FaceCameraEnabled: controller->IsFaceCameraEnabled(),
			GazeCameraEnabled: controller->IsGazeCameraEnabled());
	}

	/// <summary>Disables Face/Gaze camera tracking so manual posing is not overwritten.</summary>
	public PrepareForPosingResult PrepareForPosing()
	{
		if (!this.TryGetController(out EventGPoseController* controller))
		{
			return new PrepareForPosingResult(
				Ok: false,
				DisabledFaceCamera: false,
				DisabledGazeCamera: false,
				Error: "GPose controller unavailable.");
		}

		bool disabledFace = false;
		bool disabledGaze = false;

		if (controller->IsFaceCameraEnabled())
		{
			controller->ToggleFaceCamera();
			disabledFace = true;
		}

		if (controller->IsGazeCameraEnabled())
		{
			controller->ToggleGazeCamera();
			disabledGaze = true;
		}

		// GPose can take a frame to release look-at IK after toggling.
		if (disabledFace && controller->IsFaceCameraEnabled())
		{
			controller->ToggleFaceCamera();
		}

		if (disabledGaze && controller->IsGazeCameraEnabled())
		{
			controller->ToggleGazeCamera();
		}

		return new PrepareForPosingResult(Ok: true, disabledFace, disabledGaze, Error: null);
	}

	private bool TryGetController(out EventGPoseController* controller)
	{
		controller = null;
		EventFramework* framework = EventFramework.Instance();
		if (framework == null)
		{
			return false;
		}

		controller = (EventGPoseController*)System.Runtime.CompilerServices.Unsafe.AsPointer(
			ref framework->EventSceneModule.EventGPoseController);
		return true;
	}
}

public readonly record struct GposeCameraState(bool Available, bool FaceCameraEnabled, bool GazeCameraEnabled);

public readonly record struct PrepareForPosingResult(
	bool Ok,
	bool DisabledFaceCamera,
	bool DisabledGazeCamera,
	string? Error);
