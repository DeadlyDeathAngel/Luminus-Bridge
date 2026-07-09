// © DeadlyDeathAngel.
// Licensed under the MIT license.

namespace LuminusBridge.Services;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using System;
using System.Numerics;

/// <summary>
/// Minimal status / config UI for Dalamud plugin validation and operator feedback.
/// </summary>
public sealed class BridgeStatusWindow : Window, IDisposable
{
	private readonly Func<Configuration> getConfiguration;
	private readonly Func<GameStateSnapshot> getState;
	private readonly Func<bool> isListening;
	private readonly Action? onRestartRequested;

	private int portEdit;
	private bool enabledEdit;
	private bool autoStartOnLoginEdit;
	private bool initialized;

	public BridgeStatusWindow(
		Func<Configuration> getConfiguration,
		Func<GameStateSnapshot> getState,
		Func<bool> isListening,
		Action? onRestartRequested = null)
		: base("Luminus Bridge")
	{
		this.getConfiguration = getConfiguration;
		this.getState = getState;
		this.isListening = isListening;
		this.onRestartRequested = onRestartRequested;

		this.Size = new Vector2(420, 280);
		this.SizeCondition = ImGuiCond.FirstUseEver;
	}

	public void Dispose()
	{
		// WindowSystem owns lifetime; nothing else to free.
	}

	public override void Draw()
	{
		Configuration config = this.getConfiguration();
		if (!this.initialized)
		{
			this.portEdit = config.Port;
			this.enabledEdit = config.Enabled;
			this.autoStartOnLoginEdit = config.AutoStartOnLogin;
			this.initialized = true;
		}

		GameStateSnapshot state = this.getState();
		ImGui.TextUnformatted($"HTTP: {(this.isListening() ? "listening" : "stopped")}");
		ImGui.TextUnformatted($"Endpoint: http://{config.BindAddress}:{config.Port}/luminus/v1/");
		ImGui.Separator();
		ImGui.TextUnformatted($"Signed in: {state.SignedIn}");
		ImGui.TextUnformatted($"GPose: {state.IsInGpose}");
		ImGui.TextUnformatted($"Territory: {state.TerritoryId}");
		ImGui.Separator();

		ImGui.Checkbox("Enabled", ref this.enabledEdit);
		ImGui.Checkbox("Auto-start when signed in", ref this.autoStartOnLoginEdit);
		ImGui.InputInt("Port", ref this.portEdit);
		if (this.portEdit < 1)
		{
			this.portEdit = 1;
		}
		else if (this.portEdit > 65535)
		{
			this.portEdit = 65535;
		}

		if (ImGui.Button("Save"))
		{
			config.Enabled = this.enabledEdit;
			config.AutoStartOnLogin = this.autoStartOnLoginEdit;
			config.Port = this.portEdit;
			config.Save();
			this.onRestartRequested?.Invoke();
		}

		ImGui.SameLine();
		if (ImGui.Button("Restart HTTP"))
		{
			this.onRestartRequested?.Invoke();
		}

		ImGui.TextDisabled(
			"Idle on title screen. HTTP IPC starts automatically when signed into a territory. " +
			"Linux Luminus connects without /luminusbridge.");
	}
}
