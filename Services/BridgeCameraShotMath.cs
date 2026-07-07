// © DeadlyDeathAngel.
// Licensed under the MIT license.

namespace AnamnesisBridge.Services;

using System;
using System.Numerics;

/// <summary>Actor-relative camera shot math (matches desktop <c>CameraShotFile</c>).</summary>
internal static class BridgeCameraShotMath
{
	public static void ApplyShot(
		CameraShotData shot,
		ActorModelTransformSnapshot actor,
		out CameraUpdate update,
		out Vector3 worldPosition)
	{
		Vector3 actorEuler = FlattenActorYaw(actor.Rotation);
		Vector3 rotatedRelative = Vector3.Transform(
			shot.Position,
			Quaternion.CreateFromYawPitchRoll(
				actorEuler.Y * MathF.PI / 180f,
				actorEuler.X * MathF.PI / 180f,
				actorEuler.Z * MathF.PI / 180f));
		worldPosition = actor.Position + rotatedRelative;

		Vector3 cameraEuler = actorEuler + shot.Rotation;
		update = new CameraUpdate(
			DelimitCamera: shot.DelimitCamera,
			Zoom: shot.Zoom,
			FieldOfView: shot.FieldOfView,
			AngleXDeg: cameraEuler.Y,
			AngleYDeg: cameraEuler.Z,
			RotationDeg: cameraEuler.X,
			PanXDeg: shot.Pan.X * (180f / MathF.PI),
			PanYDeg: shot.Pan.Y * (180f / MathF.PI),
			PositionX: worldPosition.X,
			PositionY: worldPosition.Y,
			PositionZ: worldPosition.Z);
	}

	public static CameraShotData ExportShot(
		CameraSnapshot camera,
		ActorModelTransformSnapshot actor)
	{
		Vector3 actorEuler = FlattenActorYaw(actor.Rotation);
		Vector3 worldPosition = new(camera.PositionX, camera.PositionY, camera.PositionZ);
		Vector3 localRelative = worldPosition - actor.Position;
		Quaternion inverted = Quaternion.Inverse(YawQuaternion(actorEuler));
		Vector3 shotPosition = Vector3.Transform(localRelative, inverted);

		Vector3 cameraEuler = new(camera.RotationDeg, camera.AngleXDeg, camera.AngleYDeg);
		Vector3 shotRotation = cameraEuler - actorEuler;

		return new CameraShotData(
			DelimitCamera: camera.DelimitCamera,
			Zoom: camera.Zoom,
			FieldOfView: camera.FieldOfView,
			Pan: new Vector2(
				camera.PanXDeg * (MathF.PI / 180f),
				camera.PanYDeg * (MathF.PI / 180f)),
			Position: shotPosition,
			Rotation: shotRotation);
	}

	private static Vector3 FlattenActorYaw(Quaternion rotation)
	{
		Vector3 euler = QuaternionToEulerDegrees(rotation);
		euler.X = 0;
		euler.Z = 0;
		return euler;
	}

	private static Quaternion YawQuaternion(Vector3 eulerDegrees)
		=> Quaternion.CreateFromYawPitchRoll(
			eulerDegrees.Y * MathF.PI / 180f,
			eulerDegrees.X * MathF.PI / 180f,
			eulerDegrees.Z * MathF.PI / 180f);

	private static Vector3 QuaternionToEulerDegrees(Quaternion quaternion)
	{
		Vector3 v = default;
		float test = (quaternion.X * quaternion.Y) + (quaternion.Z * quaternion.W);
		if (test > 0.4995f)
		{
			v.Y = 2f * MathF.Atan2(quaternion.X, quaternion.Y);
			v.X = MathF.PI / 2f;
			v.Z = 0;
		}
		else if (test < -0.4995f)
		{
			v.Y = -2f * MathF.Atan2(quaternion.X, quaternion.W);
			v.X = -MathF.PI / 2f;
			v.Z = 0;
		}
		else
		{
			float sqx = quaternion.X * quaternion.X;
			float sqy = quaternion.Y * quaternion.Y;
			float sqz = quaternion.Z * quaternion.Z;
			v.Y = MathF.Atan2((2f * quaternion.Y * quaternion.W) - (2f * quaternion.X * quaternion.Z), 1f - (2f * sqy) - (2f * sqz));
			v.X = MathF.Asin(2f * test);
			v.Z = MathF.Atan2((2f * quaternion.X * quaternion.W) - (2f * quaternion.Y * quaternion.Z), 1f - (2f * sqx) - (2f * sqz));
		}

		const float rad2Deg = 180f / MathF.PI;
		v *= rad2Deg;
		return v;
	}
}

public readonly record struct ActorModelTransformSnapshot(Vector3 Position, Quaternion Rotation);

public readonly record struct CameraShotData(
	bool DelimitCamera,
	float Zoom,
	float FieldOfView,
	Vector2 Pan,
	Vector3 Position,
	Vector3 Rotation);
