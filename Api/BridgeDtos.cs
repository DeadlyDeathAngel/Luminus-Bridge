// © Anamnesis.
// Licensed under the MIT license.

namespace AnamnesisBridge.Api;

using System.Collections.Generic;
using System.Text.Json.Serialization;

public sealed class HealthResponse
{
	[JsonPropertyName("ok")]
	public bool Ok { get; init; } = true;

	[JsonPropertyName("plugin")]
	public string Plugin { get; init; } = "AnamnesisBridge";

	[JsonPropertyName("version")]
	public string Version { get; init; } = "0.1.0";

	[JsonPropertyName("api")]
	public int Api { get; init; } = 1;
}

public sealed class GposeResponse
{
	[JsonPropertyName("isInGpose")]
	public bool IsInGpose { get; init; }

	[JsonPropertyName("source")]
	public string Source { get; init; } = "IClientState.IsGPosing";
}

public sealed class TerritoryResponse
{
	[JsonPropertyName("territoryId")]
	public uint TerritoryId { get; init; }

	[JsonPropertyName("signedIn")]
	public bool SignedIn { get; init; }
}

public sealed class StatusResponse
{
	[JsonPropertyName("isInGpose")]
	public bool IsInGpose { get; init; }

	[JsonPropertyName("territoryId")]
	public uint TerritoryId { get; init; }

	[JsonPropertyName("signedIn")]
	public bool SignedIn { get; init; }

	[JsonPropertyName("frameworkTick")]
	public bool FrameworkTick { get; init; }
}

public sealed class ErrorResponse
{
	[JsonPropertyName("ok")]
	public bool Ok { get; init; }

	[JsonPropertyName("error")]
	public string Error { get; init; } = string.Empty;
}

public sealed class ActorsResponse
{
	[JsonPropertyName("actors")]
	public IReadOnlyList<BridgeActorDto> Actors { get; init; } = [];

	[JsonPropertyName("source")]
	public string Source { get; init; } = "IObjectTable";
}

public sealed class BridgeActorDto
{
	[JsonPropertyName("objectIndex")]
	public int ObjectIndex { get; init; }

	[JsonPropertyName("address")]
	public string Address { get; init; } = string.Empty;

	[JsonPropertyName("name")]
	public string Name { get; init; } = string.Empty;

	[JsonPropertyName("dataId")]
	public uint DataId { get; init; }

	[JsonPropertyName("objectKind")]
	public byte ObjectKind { get; init; }

	[JsonPropertyName("distance")]
	public double Distance { get; init; }
}
