// © Anamnesis.
// Licensed under the MIT license.

namespace AnamnesisBridge;

using AnamnesisBridge.Services;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

/// <summary>
/// Dalamud plugin entry: HTTP bridge for native Linux Anamnesis.
/// </summary>
public sealed class Plugin : IDalamudPlugin
{
	[PluginService]
	internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;

	[PluginService]
	internal static ICommandManager CommandManager { get; private set; } = null!;

	[PluginService]
	internal static IClientState ClientState { get; private set; } = null!;

	[PluginService]
	internal static IFramework Framework { get; private set; } = null!;

	[PluginService]
	internal static IObjectTable ObjectTable { get; private set; } = null!;

	[PluginService]
	internal static IPluginLog Log { get; private set; } = null!;

	private const string CommandName = "/anamnesisbridge";

	public Configuration Configuration { get; }

	private readonly GameStateService gameState;
	private readonly ActorEnumerationService actorEnumeration;
	private readonly BridgeHttpServer httpServer;

	public Plugin()
	{
		this.Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

		this.gameState = new GameStateService(ClientState);
		this.actorEnumeration = new ActorEnumerationService(ObjectTable);
		this.httpServer = new BridgeHttpServer(Log, this.gameState, this.actorEnumeration, () => this.Configuration);

		Framework.Update += this.OnFrameworkUpdate;
		CommandManager.AddHandler(CommandName, new CommandInfo(this.OnCommand)
		{
			HelpMessage = "Show Anamnesis Bridge HTTP status.",
		});

		this.httpServer.Restart();
		this.gameState.Update();
		Log.Information("AnamnesisBridge loaded.");
	}

	public void Dispose()
	{
		Framework.Update -= this.OnFrameworkUpdate;
		CommandManager.RemoveHandler(CommandName);
		this.httpServer.Dispose();
	}

	private void OnFrameworkUpdate(IFramework framework)
	{
		this.gameState.Update();
		this.actorEnumeration.Update();
	}

	private void OnCommand(string command, string args)
	{
		GameStateSnapshot state = this.gameState.Current;
		Log.Information(
			$"AnamnesisBridge: http://{this.Configuration.BindAddress}:{this.Configuration.Port}/anamnesis/v1/ | " +
			$"GPose={state.IsInGpose} territory={state.TerritoryId} signedIn={state.SignedIn} listening={this.httpServer.IsRunning}");
	}
}
