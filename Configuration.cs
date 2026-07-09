// © Luminus.
// Licensed under the MIT license.

namespace LuminusBridge;

using Dalamud.Configuration;
using System;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
	public int Version { get; set; } = 1;

	public bool Enabled { get; set; } = true;

	/// <summary>Start HTTP IPC automatically when signed into a territory (not title screen).</summary>
	public bool AutoStartOnLogin { get; set; } = true;

	public int Port { get; set; } = 6679;

	public string BindAddress { get; set; } = "127.0.0.1";

	public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}
