// © DeadlyDeathAngel.
// Licensed under the MIT license.

namespace LuminusBridge.Services;

using LuminusBridge.Api;
using Dalamud.Game.ClientState.Objects.Enums;
using System.Collections.Generic;

/// <summary>
/// Framework-thread snapshots for HTTP handlers (game APIs are not thread-safe).
/// </summary>
public sealed class BridgeRuntimeCache
{
	private BridgeActorDto? localPlayer;
	private BridgeActorDto? currentTarget;
	private IReadOnlyDictionary<int, ActorAppearanceDto> appearances = new Dictionary<int, ActorAppearanceDto>();

	public BridgeActorDto? LocalPlayer => this.localPlayer;

	public BridgeActorDto? CurrentTarget => this.currentTarget;

	public ActorAppearanceDto? GetAppearance(int objectIndex)
		=> this.appearances.TryGetValue(objectIndex, out ActorAppearanceDto? appearance) ? appearance : null;

	public void Update(
		BridgeTargetService targetService,
		ActorAppearanceService appearanceService,
		IReadOnlyList<BridgeActorDto> actors)
	{
		try
		{
			this.localPlayer = targetService.GetLocalPlayer();
			this.currentTarget = targetService.GetCurrentTarget();

			// Only refresh appearance for local player + current target (cheap, avoids title-screen spikes).
			var nextAppearances = new Dictionary<int, ActorAppearanceDto>();
			TryCacheAppearance(appearanceService, this.localPlayer?.ObjectIndex, nextAppearances);
			TryCacheAppearance(appearanceService, this.currentTarget?.ObjectIndex, nextAppearances);

			// Keep previously cached appearances for other pinned actors still in the list.
			foreach (BridgeActorDto actor in actors)
			{
				if (actor.ObjectKind != (byte)ObjectKind.Pc)
				{
					continue;
				}

				if (nextAppearances.ContainsKey(actor.ObjectIndex))
				{
					continue;
				}

				if (this.appearances.TryGetValue(actor.ObjectIndex, out ActorAppearanceDto? cached))
				{
					nextAppearances[actor.ObjectIndex] = cached;
				}
			}

			this.appearances = nextAppearances;
		}
		catch
		{
			// Leave previous snapshots in place on transient failures.
		}
	}

	public void RefreshAppearance(ActorAppearanceService appearanceService, int objectIndex)
	{
		try
		{
			ActorAppearanceDto? appearance = appearanceService.TryGetAppearance(objectIndex);
			if (appearance == null)
			{
				return;
			}

			var next = new Dictionary<int, ActorAppearanceDto>(this.appearances)
			{
				[objectIndex] = appearance,
			};
			this.appearances = next;
		}
		catch
		{
			// Ignore transient read failures.
		}
	}

	private static void TryCacheAppearance(
		ActorAppearanceService appearanceService,
		int? objectIndex,
		Dictionary<int, ActorAppearanceDto> destination)
	{
		if (objectIndex is not int index)
		{
			return;
		}

		ActorAppearanceDto? appearance = appearanceService.TryGetAppearance(index);
		if (appearance != null)
		{
			destination[index] = appearance;
		}
	}
}
