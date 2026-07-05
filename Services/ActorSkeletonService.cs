// © DeadlyDeathAngel.
// Licensed under the MIT license.

namespace AnamnesisBridge.Services;

using AnamnesisBridge.Api;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.Havok.Animation.Rig;
using FFXIVClientStructs.Havok.Common.Base.Math.QsTransform;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using CharacterBase = FFXIVClientStructs.FFXIV.Client.Graphics.Scene.CharacterBase;

/// <summary>
/// Control-plane skeleton read/write (0.2.7.0). Uses model-space pose arrays (same as desktop Anamnesis).
/// </summary>
public sealed unsafe class ActorSkeletonService
{
	private const int PoseApplyPasses = 3;
	private const uint ModelDirtyFlag = 1u << (int)hkaPose.BoneFlag.BoneModelDirty;
	private const uint LocalDirtyFlag = 1u << (int)hkaPose.BoneFlag.BoneLocalDirty;

	private readonly IObjectTable objectTable;

	public ActorSkeletonService(IObjectTable objectTable)
	{
		this.objectTable = objectTable;
	}

	public SkeletonResponse TryGetSkeleton(int objectIndex)
	{
		try
		{
			if (!this.TryResolveCharacter(objectIndex, out BattleChara* battleChara, out CharacterBase* charBase, out string? resolveError))
			{
				return Fail(objectIndex, resolveError ?? "Actor not found.");
			}

			Skeleton* skeleton = charBase->Skeleton;
			if (skeleton == null)
			{
				return Fail(objectIndex, "No skeleton (enter GPose for full posing data).");
			}

			var bones = new List<BoneTransformDto>(256);
			int partialCount = skeleton->PartialSkeletonCount;
			for (int partial = 0; partial < partialCount; partial++)
			{
				PartialSkeleton partialSkeleton = skeleton->PartialSkeletons[partial];
				hkaPose* pose = partialSkeleton.GetHavokPose(0);
				if (pose == null || pose->Skeleton == null)
				{
					continue;
				}

				hkaSkeleton* hkaSkeleton = pose->Skeleton;
				int boneCount = hkaSkeleton->Bones.Length;
				int modelPoseCount = pose->ModelPose.Length;
				for (int boneIndex = 0; boneIndex < boneCount; boneIndex++)
				{
					string name = $"p{partial}_{boneIndex}";
					hkQsTransformf model = default;
					bool hasTransform = boneIndex < modelPoseCount;
					if (hasTransform)
					{
						model = pose->ModelPose[boneIndex];
					}

					if (boneIndex < hkaSkeleton->Bones.Length)
					{
						hkaBone bone = hkaSkeleton->Bones[boneIndex];
						string? boneName = bone.Name.String;
						if (!string.IsNullOrWhiteSpace(boneName))
						{
							name = boneName;
						}
					}

					bones.Add(new BoneTransformDto
					{
						Name = name,
						Partial = partial,
						Index = boneIndex,
						Depth = GetBoneDepth(hkaSkeleton, boneIndex),
						PosX = hasTransform ? model.Translation.X : 0f,
						PosY = hasTransform ? model.Translation.Y : 0f,
						PosZ = hasTransform ? model.Translation.Z : 0f,
						RotX = hasTransform ? model.Rotation.X : 0f,
						RotY = hasTransform ? model.Rotation.Y : 0f,
						RotZ = hasTransform ? model.Rotation.Z : 0f,
						RotW = hasTransform ? model.Rotation.W : 1f,
						ScaleX = hasTransform ? model.Scale.X : 1f,
						ScaleY = hasTransform ? model.Scale.Y : 1f,
						ScaleZ = hasTransform ? model.Scale.Z : 1f,
					});
				}
			}

			return new SkeletonResponse
			{
				Ok = true,
				ObjectIndex = objectIndex,
				BoneCount = bones.Count,
				Bones = bones,
			};
		}
		catch (Exception ex)
		{
			return Fail(objectIndex, ex.Message);
		}
	}

	public SetBoneTransformResponse TrySetBoneTransform(int objectIndex, SetBoneTransformRequest request)
	{
		try
		{
			if (!this.TryResolveCharacter(objectIndex, out _, out CharacterBase* charBase, out string? resolveError))
			{
				return SetFail(objectIndex, request, resolveError ?? "Actor not found.");
			}

			Skeleton* skeleton = charBase->Skeleton;
			if (skeleton == null)
			{
				return SetFail(objectIndex, request, "No skeleton (enter GPose for posing).");
			}

			if (request.Partial < 0 || request.Partial >= skeleton->PartialSkeletonCount)
			{
				return SetFail(objectIndex, request, $"Invalid partial skeleton {request.Partial}.");
			}

			PartialSkeleton partialSkeleton = skeleton->PartialSkeletons[request.Partial];
			hkaPose* pose = partialSkeleton.GetHavokPose(0);
			if (pose == null)
			{
				return SetFail(objectIndex, request, "No Havok pose.");
			}

			if (request.Index < 0 || request.Index >= pose->ModelPose.Length)
			{
				return SetFail(objectIndex, request, $"Invalid bone index {request.Index}.");
			}

			hkQsTransformf transform = new()
			{
				Translation = new() { X = request.PosX, Y = request.PosY, Z = request.PosZ },
				Rotation = new() { X = request.RotX, Y = request.RotY, Z = request.RotZ, W = request.RotW },
				Scale = new() { X = request.ScaleX, Y = request.ScaleY, Z = request.ScaleZ },
			};

			this.WriteModelTransform(pose, request.Index, transform);

			return new SetBoneTransformResponse
			{
				Ok = true,
				ObjectIndex = objectIndex,
				Partial = request.Partial,
				Index = request.Index,
			};
		}
		catch (Exception ex)
		{
			return SetFail(objectIndex, request, ex.Message);
		}
	}

	public ApplyPoseResponse TryApplyPose(int objectIndex, ApplyPoseRequest request)
	{
		try
		{
			if (request.Bones.Count == 0)
			{
				return ApplyFail(objectIndex, "No bones in pose.");
			}

			SkeletonResponse skeletonRead = this.TryGetSkeleton(objectIndex);
			if (!skeletonRead.Ok)
			{
				return ApplyFail(objectIndex, skeletonRead.Error ?? "Skeleton read failed.");
			}

			var boneIndex = new Dictionary<string, BoneTransformDto>(StringComparer.Ordinal);
			foreach (BoneTransformDto bone in skeletonRead.Bones)
			{
				boneIndex.TryAdd(bone.Name, bone);
			}

			var applyList = new List<(ApplyPoseBoneDto Saved, BoneTransformDto Target)>();
			int skipped = 0;
			foreach ((string rawName, ApplyPoseBoneDto savedBone) in request.Bones)
			{
				if (string.Equals(rawName, "n_root", StringComparison.Ordinal))
				{
					skipped++;
					continue;
				}

				if (!boneIndex.TryGetValue(rawName, out BoneTransformDto? target))
				{
					skipped++;
					continue;
				}

				applyList.Add((savedBone, target));
			}

			applyList.Sort((a, b) =>
			{
				int depthCompare = a.Target.Depth.CompareTo(b.Target.Depth);
				return depthCompare != 0 ? depthCompare : string.Compare(a.Target.Name, b.Target.Name, StringComparison.Ordinal);
			});

			if (!this.TryResolveCharacter(objectIndex, out _, out CharacterBase* charBase, out string? resolveError))
			{
				return ApplyFail(objectIndex, resolveError ?? "Actor not found.");
			}

			Skeleton* skeleton = charBase->Skeleton;
			if (skeleton == null)
			{
				return ApplyFail(objectIndex, "No skeleton (enter GPose for posing).");
			}

			int applied = 0;
			for (int pass = 0; pass < PoseApplyPasses; pass++)
			{
				foreach ((ApplyPoseBoneDto savedBone, BoneTransformDto target) in applyList)
				{
					if (target.Partial < 0 || target.Partial >= skeleton->PartialSkeletonCount)
					{
						continue;
					}

					PartialSkeleton partialSkeleton = skeleton->PartialSkeletons[target.Partial];
					hkaPose* pose = partialSkeleton.GetHavokPose(0);
					if (pose == null || target.Index < 0 || target.Index >= pose->ModelPose.Length)
					{
						continue;
					}

					hkQsTransformf current = pose->ModelPose[target.Index];
					hkQsTransformf transform = new()
					{
						Translation = new()
						{
							X = request.ApplyPosition && savedBone.PosX.HasValue ? savedBone.PosX.Value : current.Translation.X,
							Y = request.ApplyPosition && savedBone.PosY.HasValue ? savedBone.PosY.Value : current.Translation.Y,
							Z = request.ApplyPosition && savedBone.PosZ.HasValue ? savedBone.PosZ.Value : current.Translation.Z,
						},
						Rotation = new()
						{
							X = request.ApplyRotation && savedBone.RotX.HasValue ? savedBone.RotX.Value : current.Rotation.X,
							Y = request.ApplyRotation && savedBone.RotY.HasValue ? savedBone.RotY.Value : current.Rotation.Y,
							Z = request.ApplyRotation && savedBone.RotZ.HasValue ? savedBone.RotZ.Value : current.Rotation.Z,
							W = request.ApplyRotation && savedBone.RotW.HasValue ? savedBone.RotW.Value : current.Rotation.W,
						},
						Scale = new()
						{
							X = request.ApplyScale && savedBone.ScaleX.HasValue ? savedBone.ScaleX.Value : current.Scale.X,
							Y = request.ApplyScale && savedBone.ScaleY.HasValue ? savedBone.ScaleY.Value : current.Scale.Y,
							Z = request.ApplyScale && savedBone.ScaleZ.HasValue ? savedBone.ScaleZ.Value : current.Scale.Z,
						},
					};

					this.WriteModelTransform(pose, target.Index, transform);
					if (pass == 0)
					{
						applied++;
					}
				}
			}

			return new ApplyPoseResponse
			{
				Ok = applied > 0,
				ObjectIndex = objectIndex,
				AppliedCount = applied,
				SkippedCount = skipped,
				Error = applied > 0 ? null : "No pose bones matched the actor skeleton.",
			};
		}
		catch (Exception ex)
		{
			return ApplyFail(objectIndex, ex.Message);
		}
	}

	private void WriteModelTransform(hkaPose* pose, int boneIndex, hkQsTransformf transform)
	{
		pose->ModelPose[boneIndex] = transform;
		pose->ModelInSync = 0;

		if (boneIndex >= 0 && boneIndex < pose->BoneFlags.Length)
		{
			pose->BoneFlags[boneIndex] &= ~ModelDirtyFlag;
			pose->BoneFlags[boneIndex] |= LocalDirtyFlag;
		}
	}

	private static int GetBoneDepth(hkaSkeleton* skeleton, int boneIndex)
	{
		int depth = 0;
		int current = boneIndex;
		while (current >= 0 && current < skeleton->ParentIndices.Length)
		{
			current = skeleton->ParentIndices[current];
			if (current < 0)
			{
				break;
			}

			depth++;
		}

		return depth;
	}

	private bool TryResolveCharacter(
		int objectIndex,
		out BattleChara* battleChara,
		out CharacterBase* charBase,
		out string? error)
	{
		battleChara = null;
		charBase = null;
		error = null;

		if (objectIndex < 0 || objectIndex >= this.objectTable.Length)
		{
			error = $"Invalid object index {objectIndex}.";
			return false;
		}

		if (this.objectTable[objectIndex] is not ICharacter character || !character.IsValid())
		{
			error = $"No character at index {objectIndex}.";
			return false;
		}

		battleChara = (BattleChara*)character.Address;
		if (battleChara == null || battleChara->DrawObject == null)
		{
			error = "No draw object.";
			return false;
		}

		charBase = (CharacterBase*)battleChara->DrawObject;
		return true;
	}

	private static SetBoneTransformResponse SetFail(int objectIndex, SetBoneTransformRequest request, string error)
		=> new()
		{
			Ok = false,
			ObjectIndex = objectIndex,
			Partial = request.Partial,
			Index = request.Index,
			Error = error,
		};

	private static SkeletonResponse Fail(int objectIndex, string error)
		=> new()
		{
			Ok = false,
			ObjectIndex = objectIndex,
			Error = error,
		};

	private static ApplyPoseResponse ApplyFail(int objectIndex, string error)
		=> new()
		{
			Ok = false,
			ObjectIndex = objectIndex,
			Error = error,
		};
}
