// © Anamnesis.
// Licensed under the MIT license.

namespace AnamnesisBridge.Services;

using AnamnesisBridge.Api;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Enumerates nearby actors via Dalamud IObjectTable (in-process, reliable on Wine/Linux).
/// </summary>
public sealed class ActorEnumerationService
{
	private readonly IObjectTable objectTable;
	private IReadOnlyList<BridgeActorDto> actors = [];

	public ActorEnumerationService(IObjectTable objectTable)
	{
		this.objectTable = objectTable;
	}

	public IReadOnlyList<BridgeActorDto> Current => this.actors;

	public void Update()
	{
		var results = new List<BridgeActorDto>();

		for (int index = 0; index < this.objectTable.Length; index++)
		{
			IGameObject? obj = this.objectTable[index];
			if (obj == null || !obj.IsValid())
			{
				continue;
			}

			if (!IsInterestingKind(obj.ObjectKind))
			{
				continue;
			}

			double distance = System.Math.Sqrt(
				(obj.YalmDistanceX * obj.YalmDistanceX) +
				(obj.YalmDistanceZ * obj.YalmDistanceZ));

			results.Add(new BridgeActorDto
			{
				ObjectIndex = obj.ObjectIndex,
				Address = $"0x{obj.Address:X}",
				Name = obj.Name.TextValue,
				DataId = obj.BaseId,
				ObjectKind = (byte)obj.ObjectKind,
				Distance = distance,
			});
		}

		this.actors = results
			.OrderBy(actor => actor.Distance)
			.ThenBy(actor => actor.ObjectIndex)
			.Take(64)
			.ToList();
	}

	private static bool IsInterestingKind(ObjectKind kind)
		=> kind is ObjectKind.Pc
			or ObjectKind.BattleNpc
			or ObjectKind.EventNpc
			or ObjectKind.Mount
			or ObjectKind.Companion
			or ObjectKind.Retainer
			or ObjectKind.AreaObject
			or ObjectKind.Ornament;
}
