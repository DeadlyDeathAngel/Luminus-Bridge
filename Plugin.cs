// © DeadlyDeathAngel.
// Licensed under the MIT license.

namespace AnamnesisBridge;

using AnamnesisBridge.Services;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using System;

/// <summary>
/// HTTP IPC for native Linux Anamnesis.
/// Idle on title screen; auto-starts when signed in (configurable). Blocking TCP accept uses no idle CPU.
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
	internal static ITargetManager TargetManager { get; private set; } = null!;

	[PluginService]
	internal static IPluginLog Log { get; private set; } = null!;

	[PluginService]
	internal static IDataManager DataManager { get; private set; } = null!;

	[PluginService]
	internal static ISigScanner SigScanner { get; private set; } = null!;

	[PluginService]
	internal static IGameInteropProvider InteropProvider { get; private set; } = null!;

	private const string CommandName = "/anamnesisbridge";
	private const int ActorCacheIntervalTicks = 60;
	private const int MaxConsecutiveFrameworkFailures = 3;
	private const int FrameworkCooldownTicks = 600;

	public Configuration Configuration { get; }

	private readonly GameStateService gameState;
	private readonly ActorEnumerationService actorEnumeration;
	private readonly BridgeTargetService targetService;
	private readonly AppearanceOverrideStore appearanceOverrides;
	private readonly ActorAppearanceService appearanceService;
	private readonly ActorRedrawService redrawService;
	private readonly ActorMotionService motionService;
	private readonly ActorSkeletonService skeletonService;
	private readonly ActorEquipmentService equipmentService;
	private readonly BridgeGameDataService gameDataService;
	private readonly BridgeCustomizeOptionsService customizeOptionsService;
	private readonly BridgePosingHooks posingHooks;
	private readonly BridgeGposeControllerService gposeControllerService;
	private readonly BridgeCameraService cameraService;
	private readonly BridgeWorldService worldService;
	private readonly BridgeIpcService ipcService;
	private readonly BridgeRuntimeCache runtimeCache;
	private readonly FrameworkThreadDispatcher frameworkDispatcher;
	private readonly BridgeHttpServer httpServer;
	private readonly WindowSystem windowSystem = new("AnamnesisBridge");
	private readonly BridgeStatusWindow statusWindow;

	private bool frameworkHooked;
	private bool frameworkHookLogged;
	private bool drawHooked;
	private int actorCacheTickCounter;
	private int consecutiveFrameworkFailures;
	private int frameworkCooldownRemaining;

	public Plugin()
	{
		this.Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

		this.gameState = new GameStateService(ClientState);
		this.actorEnumeration = new ActorEnumerationService(ClientState, ObjectTable);
		this.targetService = new BridgeTargetService(ClientState, ObjectTable, TargetManager);
		this.appearanceOverrides = new AppearanceOverrideStore();
		this.appearanceService = new ActorAppearanceService(ObjectTable, this.appearanceOverrides);
		this.redrawService = new ActorRedrawService(this.appearanceService);
		this.motionService = new ActorMotionService(ObjectTable, this.gameState);
		this.skeletonService = new ActorSkeletonService(ObjectTable);
		this.gameDataService = new BridgeGameDataService(DataManager, PluginInterface, Log);
		this.customizeOptionsService = new BridgeCustomizeOptionsService(DataManager, Log);
		this.equipmentService = new ActorEquipmentService(ObjectTable, this.gameDataService, Log);
		this.gposeControllerService = new BridgeGposeControllerService();
		this.cameraService = new BridgeCameraService(SigScanner, InteropProvider, Log, ClientState, this.skeletonService);
		this.worldService = new BridgeWorldService(SigScanner, Log, ClientState, this.gameDataService);
		this.posingHooks = new BridgePosingHooks(
			SigScanner,
			InteropProvider,
			Log,
			() => ClientState.IsGPosing,
			this.AreGposePosingHooksAllowed);
		this.ipcService = new BridgeIpcService(this.gameState, this.posingHooks, this.gposeControllerService);
		this.ipcService.SetPosingHooksEngagedProvider(this.AreGposePosingHooksAllowed);
		this.runtimeCache = new BridgeRuntimeCache();
		this.frameworkDispatcher = new FrameworkThreadDispatcher(Framework);
		this.httpServer = new BridgeHttpServer(
			Log,
			this.gameState,
			this.actorEnumeration,
			this.targetService,
			this.appearanceService,
			this.redrawService,
			this.motionService,
			this.skeletonService,
			this.equipmentService,
			this.gameDataService,
			this.customizeOptionsService,
			this.ipcService,
			this.gposeControllerService,
			this.cameraService,
			this.worldService,
			this.runtimeCache,
			this.frameworkDispatcher,
			() => this.Configuration);
		this.httpServer.ClientActivity += this.OnClientActivity;

		this.statusWindow = new BridgeStatusWindow(
			() => this.Configuration,
			() => this.gameState.Current,
			() => this.httpServer.IsRunning,
			this.SyncSessionFromConfiguration);
		this.windowSystem.AddWindow(this.statusWindow);

		ClientState.Login += this.OnLogin;
		ClientState.Logout += this.OnLogout;

		PluginInterface.UiBuilder.OpenMainUi += this.OnOpenMainUi;
		PluginInterface.UiBuilder.OpenConfigUi += this.OnOpenConfigUi;
		CommandManager.AddHandler(CommandName, new CommandInfo(this.OnCommand)
		{
			HelpMessage = "Open Anamnesis Bridge status (HTTP IPC auto-starts when signed in).",
		});

		this.gameState.Update();
		this.ipcService.InitializeGposeState(this.gameState.Current.IsInGpose);
		this.TryAutoStartSession();
		Log.Information(
			"AnamnesisBridge loaded. " +
			(this.httpServer.IsRunning
				? $"Listening on http://127.0.0.1:{this.Configuration.Port}/anamnesis/v1/"
				: "Idle on title screen; auto-starts when signed in."));
	}

	public void Dispose()
	{
		ClientState.Login -= this.OnLogin;
		ClientState.Logout -= this.OnLogout;
		this.httpServer.ClientActivity -= this.OnClientActivity;
		this.ipcService.ResetSessionState();
		this.StopSession();
		this.StopDrawHook();
		this.httpServer.Dispose();
		this.worldService.Dispose();
		this.cameraService.Dispose();
		this.posingHooks.Dispose();

		PluginInterface.UiBuilder.OpenMainUi -= this.OnOpenMainUi;
		PluginInterface.UiBuilder.OpenConfigUi -= this.OnOpenConfigUi;
		CommandManager.RemoveHandler(CommandName);
		this.windowSystem.RemoveAllWindows();
		this.statusWindow.Dispose();
	}

	private void OnLogin()
	{
		this.ipcService.ResetSessionState();

		// Territory may not be ready yet; framework hook polls until signed in.
		if (!this.Configuration.Enabled || !this.Configuration.AutoStartOnLogin)
		{
			return;
		}

		this.StartFrameworkHook();
		this.TryAutoStartSession();
	}

	private void OnLogout(int type, int code)
	{
		this.ipcService.ResetSessionState();
		this.StopSession();
	}

	private void StartListener()
	{
		if (!this.httpServer.IsRunning)
		{
			this.httpServer.Restart();
		}
	}

	private void StopListener()
	{
		if (this.httpServer.IsRunning)
		{
			this.httpServer.Stop();
		}
	}

	private void SyncSessionFromConfiguration()
	{
		if (!this.Configuration.Enabled)
		{
			this.StopSession();
			return;
		}

		if (this.httpServer.IsRunning)
		{
			this.httpServer.Restart();
		}
		else
		{
			this.TryAutoStartSession();
		}
	}

	private void TryAutoStartSession()
	{
		if (!this.Configuration.Enabled || !this.Configuration.AutoStartOnLogin)
		{
			return;
		}

		this.gameState.Update();
		if (!this.gameState.Current.SignedIn)
		{
			return;
		}

		this.StartListener();
		this.StartFrameworkHook();
	}

	private void StopSession()
	{
		bool wasListening = this.httpServer.IsRunning;
		this.StopFrameworkHook();
		this.StopListener();
		if (wasListening)
		{
			Log.Information("AnamnesisBridge idle (logged out).");
		}
	}

	private void OnClientActivity()
	{
		this.StartFrameworkHook();
	}

	private void StartFrameworkHook()
	{
		if (this.frameworkHooked)
		{
			return;
		}

		this.frameworkHooked = true;
		this.frameworkHookLogged = false;
		this.gameState.Update();
		Framework.Update += this.OnFrameworkUpdate;
	}

	private void StopFrameworkHook()
	{
		if (!this.frameworkHooked)
		{
			return;
		}

		this.frameworkHooked = false;
		this.frameworkHookLogged = false;
		Framework.Update -= this.OnFrameworkUpdate;
		this.actorCacheTickCounter = 0;
		this.consecutiveFrameworkFailures = 0;
		this.frameworkCooldownRemaining = 0;
	}

	private void LogFrameworkHookEnabled()
	{
		if (this.frameworkHookLogged)
		{
			return;
		}

		this.frameworkHookLogged = true;
		string reason = this.httpServer.HasRecentClientActivity
			? "client connected"
			: "signed in";
		Log.Information($"AnamnesisBridge framework polling enabled ({reason}).");
	}

	private void StartDrawHook()
	{
		if (this.drawHooked)
		{
			return;
		}

		this.drawHooked = true;
		PluginInterface.UiBuilder.Draw += this.OnDraw;
	}

	private void StopDrawHook()
	{
		if (!this.drawHooked)
		{
			return;
		}

		this.drawHooked = false;
		PluginInterface.UiBuilder.Draw -= this.OnDraw;
	}

	private void OnDraw()
	{
		if (!this.statusWindow.IsOpen)
		{
			this.StopDrawHook();
			return;
		}

		this.windowSystem.Draw();
	}

	private void OnOpenMainUi()
	{
		this.TryAutoStartSession();
		if (!this.httpServer.IsRunning && this.Configuration.Enabled)
		{
			this.StartListener();
			this.StartFrameworkHook();
		}

		this.statusWindow.IsOpen = true;
		this.StartDrawHook();
	}

	private void OnOpenConfigUi()
	{
		this.OnOpenMainUi();
	}

	private int gposeSceneSettleTicksRemaining;

	private const int GposeSceneSettleTicks = 120;

	private bool AreGposePosingHooksAllowed()
		=> ClientState.IsGPosing
			&& this.gposeSceneSettleTicksRemaining <= 0
			&& this.ipcService.ArePosingHooksAllowed();

	private void OnFrameworkUpdate(IFramework framework)
	{
		try
		{
			this.gameState.Update();
			GameStateSnapshot state = this.gameState.Current;
			bool inGpose = state.IsInGpose;

			this.ipcService.OnFrameworkTick(inGpose);
			this.cameraService.OnFrameworkTick(inGpose);
			this.worldService.OnFrameworkTick(inGpose);

			if (this.ipcService.UpdateGposeState(inGpose, out bool enteredGpose))
			{
				Log.Information("Left GPose — posing state reset (no redraw).");
			}
			else if (enteredGpose)
			{
				this.gposeSceneSettleTicksRemaining = GposeSceneSettleTicks;
				Log.Information(
					$"Entered GPose — scene settle ({GposeSceneSettleTicks} ticks) before posing hooks.");
			}

			if (!inGpose)
			{
				this.gposeSceneSettleTicksRemaining = 0;
			}
			else if (this.gposeSceneSettleTicksRemaining > 0)
			{
				this.gposeSceneSettleTicksRemaining--;
			}

			if (this.Configuration.Enabled && this.Configuration.AutoStartOnLogin && state.SignedIn && !this.httpServer.IsRunning)
			{
				this.StartListener();
			}

			bool keepFrameworkWork = state.SignedIn || this.httpServer.HasRecentClientActivity;
			if (!keepFrameworkWork)
			{
				this.StopFrameworkHook();
				if (!state.SignedIn)
				{
					this.StopListener();
				}

				return;
			}

			this.LogFrameworkHookEnabled();

			if (this.frameworkCooldownRemaining > 0)
			{
				this.frameworkCooldownRemaining--;
				return;
			}

			// Height/skin are overwritten by the game every frame — re-apply continuously.
			this.appearanceService.ReapplyOverrides();

			this.actorCacheTickCounter++;
			if (this.actorCacheTickCounter < ActorCacheIntervalTicks)
			{
				return;
			}

			this.actorCacheTickCounter = 0;
			this.actorEnumeration.Update();
			this.runtimeCache.Update(this.targetService, this.appearanceService, this.actorEnumeration.Current);
			this.consecutiveFrameworkFailures = 0;
		}
		catch (Exception ex)
		{
			this.consecutiveFrameworkFailures++;
			Log.Warning(ex, "AnamnesisBridge framework update failed.");
			if (this.consecutiveFrameworkFailures >= MaxConsecutiveFrameworkFailures)
			{
				this.frameworkCooldownRemaining = FrameworkCooldownTicks;
				this.consecutiveFrameworkFailures = 0;
			}
		}
	}

	private void OnCommand(string command, string args)
	{
		this.TryAutoStartSession();
		if (!this.httpServer.IsRunning && this.Configuration.Enabled)
		{
			this.StartListener();
			this.StartFrameworkHook();
		}

		this.statusWindow.IsOpen = true;
		this.StartDrawHook();
		GameStateSnapshot state = this.gameState.Current;
		Log.Information(
			$"AnamnesisBridge: http://127.0.0.1:{this.Configuration.Port}/anamnesis/v1/ | " +
			$"listening={this.httpServer.IsRunning} signedIn={state.SignedIn}");
	}
}
