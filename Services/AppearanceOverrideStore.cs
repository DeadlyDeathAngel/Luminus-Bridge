// © DeadlyDeathAngel.
// Licensed under the MIT license.

namespace AnamnesisBridge.Services;

using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Per-actor visual overrides that the game resets every frame (model scale, skin shader).
/// Re-applied on the framework thread while the Linux client is active.
/// </summary>
public sealed class AppearanceOverrideStore
{
	private readonly Dictionary<int, AppearanceOverride> overrides = new();
	private readonly object gate = new();

	public void Set(int objectIndex, byte height, byte skinTone)
	{
		lock (this.gate)
		{
			this.overrides[objectIndex] = new AppearanceOverride(height, skinTone);
		}
	}

	public void Clear(int objectIndex)
	{
		lock (this.gate)
		{
			this.overrides.Remove(objectIndex);
		}
	}

	public IReadOnlyList<KeyValuePair<int, AppearanceOverride>> Snapshot()
	{
		lock (this.gate)
		{
			return this.overrides.ToList();
		}
	}

	public readonly record struct AppearanceOverride(byte Height, byte SkinTone);
}
