// © DeadlyDeathAngel.
// Licensed under the MIT license.

namespace LuminusBridge.GameData;

using System.Text.Json;

/// <summary>
/// Matches desktop <c>SerializerService.Options</c> for embedded Luminus data files.
/// </summary>
internal static class BridgeEmbeddedJson
{
	public static readonly JsonDocumentOptions DocumentOptions = new()
	{
		AllowTrailingCommas = true,
		CommentHandling = JsonCommentHandling.Skip,
	};

	public static readonly JsonSerializerOptions SerializerOptions = new()
	{
		AllowTrailingCommas = true,
		ReadCommentHandling = JsonCommentHandling.Skip,
	};
}
