// © DeadlyDeathAngel.
// Licensed under the MIT license.

namespace LuminusBridge.Services;

using LuminusBridge.Api;
using LuminusBridge.Pose;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.Havok.Animation.Rig;
using FFXIVClientStructs.Havok.Common.Base.Math.QsTransform;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using CharacterBase = FFXIVClientStructs.FFXIV.Client.Graphics.Scene.CharacterBase;

/// <summary>
/// Control-plane skeleton read/write (0.2.7.0). Uses model-space pose arrays (same as desktop Luminus).
/// </summary>
public sealed unsafe class ActorSkeletonService
{
	private const int PoseApplyPasses = 3;
	private const int ActorTransformOffset = 0x50;
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
						try
						{
							hkaBone bone = hkaSkeleton->Bones[boneIndex];
							string? boneName = bone.Name.String;
							if (!string.IsNullOrWhiteSpace(boneName))
							{
								name = boneName;
							}
						}
						catch
						{
							// Keep fallback name when Havok string read fails mid-pose.
						}
					}

					bones.Add(new BoneTransformDto
					{
						Name = name,
						Partial = partial,
						Index = boneIndex,
						Depth = GetBoneDepth(hkaSkeleton, boneIndex),
						PosX = SanitizeFloat(hasTransform ? model.Translation.X : 0f),
						PosY = SanitizeFloat(hasTransform ? model.Translation.Y : 0f),
						PosZ = SanitizeFloat(hasTransform ? model.Translation.Z : 0f),
						RotX = SanitizeFloat(hasTransform ? model.Rotation.X : 0f),
						RotY = SanitizeFloat(hasTransform ? model.Rotation.Y : 0f),
						RotZ = SanitizeFloat(hasTransform ? model.Rotation.Z : 0f),
						RotW = SanitizeFloat(hasTransform ? model.Rotation.W : 1f),
						ScaleX = SanitizeFloat(hasTransform ? model.Scale.X : 1f),
						ScaleY = SanitizeFloat(hasTransform ? model.Scale.Y : 1f),
						ScaleZ = SanitizeFloat(hasTransform ? model.Scale.Z : 1f),
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
		if (request.ApplyModelTransform)
		{
			if (!this.TryResolveCharacter(objectIndex, out _, out CharacterBase* charBase, out string? resolveError))
			{
				return ApplyFail(objectIndex, resolveError ?? "Actor not found.");
			}

			if (!this.TryApplyModelDifference(charBase, request, out string? transformError))
			{
				return ApplyFail(objectIndex, transformError ?? "Model transform failed.");
			}
		}

		if (request.CharacterTwoPass)
		{
			return this.TryApplyCharacterPose(objectIndex, request);
		}

		return this.TryApplyPoseInternal(objectIndex, request);
	}

	public ApplyPoseResponse TryApplyCharacterPose(int objectIndex, ApplyPoseRequest request)
	{
		try
		{
			if (request.Bones.Count == 0)
			{
				return ApplyFail(objectIndex, "No bones in pose.");
			}

			var bodyBones = new Dictionary<string, ApplyPoseBoneDto>(StringComparer.Ordinal);
			var expressionBones = new Dictionary<string, ApplyPoseBoneDto>(StringComparer.Ordinal);
			foreach ((string name, ApplyPoseBoneDto bone) in request.Bones)
			{
				if (PoseBoneFilter.ShouldInclude(name, PoseImportScope.BodyOnly, request.BrioStyleBodyPass))
				{
					bodyBones[name] = bone;
				}

				if (!request.SkipExpressionPass
					&& PoseBoneFilter.ShouldInclude(name, PoseImportScope.ExpressionOnly))
				{
					expressionBones[name] = request.ExpressionApplyPosition
						? bone
						: StripPositions(bone);
				}
			}

			if (bodyBones.Count == 0)
			{
				return ApplyFail(objectIndex, "No body bones matched the character filter.");
			}

			ApplyPoseResponse bodyResponse = this.TryApplyPoseInternal(
				objectIndex,
				new ApplyPoseRequest
				{
					Bones = bodyBones,
					ApplyPosition = request.ApplyPosition,
					ApplyRotation = request.ApplyRotation,
					ApplyScale = request.ApplyScale,
					RestoreHeadAfterApply = false,
				});

			if (!bodyResponse.Ok)
			{
				return bodyResponse;
			}

			if (expressionBones.Count == 0)
			{
				return bodyResponse;
			}

			ApplyPoseResponse expressionResponse = this.TryApplyPoseInternal(
				objectIndex,
				new ApplyPoseRequest
				{
					Bones = expressionBones,
					ApplyPosition = request.ApplyPosition,
					ApplyRotation = request.ApplyRotation,
					ApplyScale = request.ApplyScale,
					RestoreHeadAfterApply = request.RestoreHeadAfterApply,
				});

			if (!expressionResponse.Ok)
			{
				return expressionResponse;
			}

			int totalApplied = bodyResponse.AppliedCount + expressionResponse.AppliedCount;
			int totalSkipped = bodyResponse.SkippedCount + expressionResponse.SkippedCount;

			if (request.ApplyPosition && request.HairPositionPass)
			{
				var hairBones = new Dictionary<string, ApplyPoseBoneDto>(StringComparer.Ordinal);
				foreach ((string name, ApplyPoseBoneDto bone) in request.Bones)
				{
					if (PoseBoneFilter.IsHairRigBone(name))
					{
						hairBones[name] = bone;
					}
				}

				if (hairBones.Count > 0)
				{
					ApplyPoseResponse hairResponse = this.TryApplyPoseInternal(
						objectIndex,
						new ApplyPoseRequest
						{
							Bones = hairBones,
							ApplyPosition = request.ApplyPosition,
							ApplyRotation = request.ApplyRotation,
							ApplyScale = request.ApplyScale,
							RestoreHeadAfterApply = false,
						});

					if (!hairResponse.Ok)
					{
						return hairResponse;
					}

					totalApplied += hairResponse.AppliedCount;
					totalSkipped += hairResponse.SkippedCount;
				}
			}

			return new ApplyPoseResponse
			{
				Ok = true,
				ObjectIndex = objectIndex,
				AppliedCount = totalApplied,
				SkippedCount = totalSkipped,
			};
		}
		catch (Exception ex)
		{
			return ApplyFail(objectIndex, ex.Message);
		}
	}

	private ApplyPoseResponse TryApplyPoseInternal(int objectIndex, ApplyPoseRequest request)
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
				string boneName = LegacyBoneNameConverter.GetModernName(rawName) ?? rawName;
				if (string.Equals(boneName, "n_root", StringComparison.Ordinal)
					|| string.Equals(boneName, "n_throw", StringComparison.Ordinal))
				{
					skipped++;
					continue;
				}

				BoneTransformDto? target = null;
				if (!boneIndex.TryGetValue(boneName, out target)
					&& !boneIndex.TryGetValue(rawName, out target))
				{
					string? resolved = PoseHairBoneResolver.ResolveTargetBoneName(rawName, boneIndex);
					if (resolved != null)
					{
						boneIndex.TryGetValue(resolved, out target);
					}
				}

				if (target == null)
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

			hkQsTransformf? savedHeadTransform = null;
			int savedHeadPartial = -1;
			int savedHeadIndex = -1;
			if (request.RestoreHeadAfterApply
				&& boneIndex.TryGetValue("j_kao", out BoneTransformDto? headTarget))
			{
				if (this.TryReadModelTransform(skeleton, headTarget, out hkQsTransformf headTransform))
				{
					savedHeadTransform = headTransform;
					savedHeadPartial = headTarget.Partial;
					savedHeadIndex = headTarget.Index;
				}
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
					Quaternion finalRotation = new(
						current.Rotation.X,
						current.Rotation.Y,
						current.Rotation.Z,
						current.Rotation.W);
					if (request.ApplyRotation && savedBone.RotX.HasValue)
					{
						finalRotation = Quaternion.Normalize(new Quaternion(
							savedBone.RotX.Value,
							savedBone.RotY ?? 0f,
							savedBone.RotZ ?? 0f,
							savedBone.RotW ?? 1f));
					}

					Vector3 finalTranslation = new(
						current.Translation.X,
						current.Translation.Y,
						current.Translation.Z);
					if (request.ApplyPosition && savedBone.PosX.HasValue)
					{
						finalTranslation = new Vector3(
							savedBone.PosX.Value,
							savedBone.PosY ?? 0f,
							savedBone.PosZ ?? 0f);
					}

					Vector3 finalScale = new(current.Scale.X, current.Scale.Y, current.Scale.Z);
					if (request.ApplyScale && savedBone.ScaleX.HasValue)
					{
						finalScale = new Vector3(
							savedBone.ScaleX.Value,
							savedBone.ScaleY ?? 1f,
							savedBone.ScaleZ ?? 1f);
					}

					hkQsTransformf transform = new()
					{
						Translation = new()
						{
							X = finalTranslation.X,
							Y = finalTranslation.Y,
							Z = finalTranslation.Z,
						},
						Rotation = new()
						{
							X = finalRotation.X,
							Y = finalRotation.Y,
							Z = finalRotation.Z,
							W = finalRotation.W,
						},
						Scale = new()
						{
							X = finalScale.X,
							Y = finalScale.Y,
							Z = finalScale.Z,
						},
					};

					this.WriteModelTransform(pose, target.Index, transform);
					if (pass == 0)
					{
						applied++;
					}
				}
			}

			if (savedHeadTransform.HasValue
				&& savedHeadPartial >= 0
				&& savedHeadIndex >= 0
				&& savedHeadPartial < skeleton->PartialSkeletonCount)
			{
				PartialSkeleton headPartialSkeleton = skeleton->PartialSkeletons[savedHeadPartial];
				hkaPose* headPose = headPartialSkeleton.GetHavokPose(0);
				if (headPose != null
					&& savedHeadIndex >= 0
					&& savedHeadIndex < headPose->ModelPose.Length)
				{
					this.WriteModelTransform(headPose, savedHeadIndex, savedHeadTransform.Value);
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

	private unsafe bool TryReadModelTransform(Skeleton* skeleton, BoneTransformDto bone, out hkQsTransformf transform)
	{
		transform = default;
		if (bone.Partial < 0 || bone.Partial >= skeleton->PartialSkeletonCount)
		{
			return false;
		}

		PartialSkeleton partialSkeleton = skeleton->PartialSkeletons[bone.Partial];
		hkaPose* pose = partialSkeleton.GetHavokPose(0);
		if (pose == null || bone.Index < 0 || bone.Index >= pose->ModelPose.Length)
		{
			return false;
		}

		transform = pose->ModelPose[bone.Index];
		return true;
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

	/// <summary>Marks skeleton poses dirty so animation can take over after GPose posing ends.</summary>
	public int TryReleaseAllCharacterSkeletons()
	{
		int released = 0;
		int length = this.objectTable.Length;
		for (int objectIndex = 0; objectIndex < length; objectIndex++)
		{
			if (this.TryReleaseSkeletonAnimation(objectIndex))
			{
				released++;
			}
		}

		return released;
	}

	public bool TryGetModelTransform(
		int objectIndex,
		out ActorModelTransformSnapshot transform,
		out string? error)
	{
		transform = default;
		if (!this.TryResolveCharacter(objectIndex, out _, out CharacterBase* charBase, out error))
		{
			return false;
		}

		ActorSceneTransform* sceneTransform = (ActorSceneTransform*)((byte*)charBase + ActorTransformOffset);
		transform = new ActorModelTransformSnapshot(sceneTransform->Position, sceneTransform->Rotation);
		return true;
	}

	public bool TryReleaseSkeletonAnimation(int objectIndex)
	{
		try
		{
			if (!this.TryResolveCharacter(objectIndex, out _, out CharacterBase* charBase, out _))
			{
				return false;
			}

			Skeleton* skeleton = charBase->Skeleton;
			if (skeleton == null)
			{
				return false;
			}

			bool released = false;
			int partialCount = skeleton->PartialSkeletonCount;
			for (int partial = 0; partial < partialCount; partial++)
			{
				PartialSkeleton partialSkeleton = skeleton->PartialSkeletons[partial];
				hkaPose* pose = partialSkeleton.GetHavokPose(0);
				if (pose == null || pose->BoneFlags.Length <= 0)
				{
					continue;
				}

				pose->ModelInSync = 0;
				pose->LocalInSync = 0;
				int boneCount = pose->BoneFlags.Length;
				for (int boneIndex = 0; boneIndex < boneCount; boneIndex++)
				{
					pose->BoneFlags[boneIndex] |= ModelDirtyFlag | LocalDirtyFlag;
				}

				released = true;
			}

			return released;
		}
		catch
		{
			return false;
		}
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

	private static ApplyPoseBoneDto StripPositions(ApplyPoseBoneDto bone)
		=> new()
		{
			PosX = null,
			PosY = null,
			PosZ = null,
			RotX = bone.RotX,
			RotY = bone.RotY,
			RotZ = bone.RotZ,
			RotW = bone.RotW,
			ScaleX = bone.ScaleX,
			ScaleY = bone.ScaleY,
			ScaleZ = bone.ScaleZ,
		};

	private static float SanitizeFloat(float value)
		=> float.IsFinite(value) ? value : 0f;

	[StructLayout(LayoutKind.Sequential)]
	private struct ActorSceneTransform
	{
		public Vector3 Position;

		public Quaternion Rotation;

		public Vector3 Scale;
	}

	private unsafe bool TryApplyModelDifference(
		CharacterBase* charBase,
		ApplyPoseRequest request,
		out string? error)
	{
		error = null;
		ActorSceneTransform* transform = (ActorSceneTransform*)((byte*)charBase + ActorTransformOffset);
		ActorSceneTransform current = *transform;

		if (request.ModelDiffPosX.HasValue || request.ModelDiffPosY.HasValue || request.ModelDiffPosZ.HasValue)
		{
			current.Position += new Vector3(
				request.ModelDiffPosX ?? 0f,
				request.ModelDiffPosY ?? 0f,
				request.ModelDiffPosZ ?? 0f);
		}

		if (request.ModelDiffRotX.HasValue
			|| request.ModelDiffRotY.HasValue
			|| request.ModelDiffRotZ.HasValue
			|| request.ModelDiffRotW.HasValue)
		{
			var delta = new Quaternion(
				request.ModelDiffRotX ?? 0f,
				request.ModelDiffRotY ?? 0f,
				request.ModelDiffRotZ ?? 0f,
				request.ModelDiffRotW ?? 1f);
			current.Rotation = Quaternion.Normalize(current.Rotation * delta);
		}

		if (request.ModelDiffScaleX.HasValue || request.ModelDiffScaleY.HasValue || request.ModelDiffScaleZ.HasValue)
		{
			current.Scale += new Vector3(
				request.ModelDiffScaleX ?? 0f,
				request.ModelDiffScaleY ?? 0f,
				request.ModelDiffScaleZ ?? 0f);
		}

		*transform = current;
		return true;
	}
}
