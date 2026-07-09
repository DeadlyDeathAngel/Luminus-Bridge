// © DeadlyDeathAngel.
// Licensed under the MIT license.

namespace LuminusBridge.Services;

using LuminusBridge.Api;
using Dalamud.Plugin.Services;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;

/// <summary>
/// Localhost HTTP server for native Linux Luminus.
/// Dedicated thread + blocking Accept (no async spin under Wine).
/// </summary>
public sealed class BridgeHttpServer : IDisposable
{
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		WriteIndented = false,
		NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
	};

	private readonly IPluginLog log;
	private readonly GameStateService gameState;
	private readonly ActorEnumerationService actors;
	private readonly BridgeTargetService targetService;
	private readonly ActorAppearanceService appearanceService;
	private readonly ActorRedrawService redrawService;
	private readonly ActorMotionService motionService;
	private readonly ActorAnimationService animationService;
	private readonly ActorSkeletonService skeletonService;
	private readonly ActorEquipmentService equipmentService;
	private readonly BridgeGameDataService gameDataService;
	private readonly BridgeCustomizeOptionsService customizeOptionsService;
	private readonly BridgeIpcService ipcService;
	private readonly BridgeGposeControllerService gposeControllerService;
	private readonly BridgeCameraService cameraService;
	private readonly BridgeWorldService worldService;
	private readonly BridgeRuntimeCache runtimeCache;
	private readonly FrameworkThreadDispatcher frameworkDispatcher;
	private readonly Func<Configuration> getConfiguration;

	private TcpListener? listener;
	private Thread? listenThread;
	private volatile bool running;
	private long lastClientActivityTicks;
	private int port = 6679;

	public event Action? ClientActivity;

	public BridgeHttpServer(
		IPluginLog log,
		GameStateService gameState,
		ActorEnumerationService actors,
		BridgeTargetService targetService,
		ActorAppearanceService appearanceService,
		ActorRedrawService redrawService,
		ActorMotionService motionService,
		ActorAnimationService animationService,
		ActorSkeletonService skeletonService,
		ActorEquipmentService equipmentService,
		BridgeGameDataService gameDataService,
		BridgeCustomizeOptionsService customizeOptionsService,
		BridgeIpcService ipcService,
		BridgeGposeControllerService gposeControllerService,
		BridgeCameraService cameraService,
		BridgeWorldService worldService,
		BridgeRuntimeCache runtimeCache,
		FrameworkThreadDispatcher frameworkDispatcher,
		Func<Configuration> getConfiguration)
	{
		this.log = log;
		this.gameState = gameState;
		this.actors = actors;
		this.targetService = targetService;
		this.appearanceService = appearanceService;
		this.redrawService = redrawService;
		this.motionService = motionService;
		this.animationService = animationService;
		this.skeletonService = skeletonService;
		this.equipmentService = equipmentService;
		this.gameDataService = gameDataService;
		this.customizeOptionsService = customizeOptionsService;
		this.ipcService = ipcService;
		this.gposeControllerService = gposeControllerService;
		this.cameraService = cameraService;
		this.worldService = worldService;
		this.runtimeCache = runtimeCache;
		this.frameworkDispatcher = frameworkDispatcher;
		this.getConfiguration = getConfiguration;
	}

	public bool IsRunning => this.running;

	public bool HasRecentClientActivity
	{
		get
		{
			long last = Interlocked.Read(ref this.lastClientActivityTicks);
			return last != 0 && (DateTime.UtcNow.Ticks - last) < TimeSpan.FromSeconds(15).Ticks;
		}
	}

	public void Restart()
	{
		this.Stop();
		Configuration config = this.getConfiguration();
		if (!config.Enabled)
		{
			return;
		}

		this.port = config.Port > 0 ? config.Port : 6679;
		var tcp = new TcpListener(IPAddress.Loopback, this.port);
		tcp.Server.NoDelay = true;
		tcp.Start(backlog: 2);
		this.listener = tcp;
		this.running = true;

		this.listenThread = new Thread(this.ListenLoop)
		{
			IsBackground = true,
			Name = "LuminusBridge.Http",
			Priority = ThreadPriority.BelowNormal,
		};
		this.listenThread.Start();

		this.log.Information($"LuminusBridge listening on http://127.0.0.1:{this.port}/luminus/v1/");
	}

	public void Stop()
	{
		this.running = false;

		try
		{
			this.listener?.Stop();
		}
		catch
		{
			// Best effort.
		}

		this.listener = null;

		Thread? thread = this.listenThread;
		this.listenThread = null;
		if (thread != null && thread.IsAlive && !thread.Join(500))
		{
			// Listener thread is blocked in Accept; Stop() unblocks it.
		}

		Interlocked.Exchange(ref this.lastClientActivityTicks, 0);
	}

	public void Dispose() => this.Stop();

	private void ListenLoop()
	{
		while (this.running)
		{
			TcpListener? local = this.listener;
			if (local == null)
			{
				break;
			}

			try
			{
				// Blocking accept — sleeps in the kernel until a client connects (0% idle CPU).
				TcpClient client = local.AcceptTcpClient();
				client.NoDelay = true;
				ThreadPool.QueueUserWorkItem(static state =>
				{
					var (server, tcpClient) = ((BridgeHttpServer, TcpClient))state!;
					server.HandleClient(tcpClient);
				}, (this, client));
			}
			catch (SocketException) when (!this.running)
			{
				break;
			}
			catch (ObjectDisposedException)
			{
				break;
			}
			catch (Exception ex)
			{
				if (this.running)
				{
					this.log.Warning(ex, "LuminusBridge accept error.");
					Thread.Sleep(100);
				}
			}
		}
	}

	private void HandleClient(TcpClient client)
	{
		using (client)
		using (NetworkStream stream = client.GetStream())
		{
			try
			{
				stream.ReadTimeout = 2000;
				stream.WriteTimeout = 2000;

				if (!TryReadHttpRequest(stream, out string method, out string path, out string body))
				{
					return;
				}

				Interlocked.Exchange(ref this.lastClientActivityTicks, DateTime.UtcNow.Ticks);
				try
				{
					this.ClientActivity?.Invoke();
				}
				catch
				{
					// Never let subscriber failures kill the request.
				}

				this.Dispatch(method, path, body, stream);
			}
			catch (Exception ex)
			{
				this.log.Warning(ex, "LuminusBridge request failed.");
				try
				{
					this.WriteJson(stream, 500, new ErrorResponse { Ok = false, Error = ex.Message });
				}
				catch
				{
					// Ignore secondary failures.
				}
			}
		}
	}

	private void Dispatch(string method, string path, string body, Stream stream)
	{
		string queryString = string.Empty;
		int queryIndex = path.IndexOf('?', StringComparison.Ordinal);
		if (queryIndex >= 0)
		{
			queryString = path[(queryIndex + 1)..];
			path = path[..queryIndex];
		}

		path = path.TrimEnd('/');

		if (path.EndsWith("/health", StringComparison.OrdinalIgnoreCase))
		{
			// Wake game-state snapshot so signedIn is accurate (Linux client uses /health to attach).
			this.frameworkDispatcher.TryRun(this.gameState.Update, out _);
			GameStateSnapshot healthState = this.gameState.Current;
			this.WriteJson(stream, 200, new HealthResponse
			{
				Version = BridgeVersion.Current,
				SignedIn = healthState.SignedIn,
				IsInGpose = healthState.IsInGpose,
				TerritoryId = healthState.TerritoryId,
				Listening = true,
			});
			return;
		}

		// Ensure snapshot exists even before the first framework tick.
		GameStateSnapshot state = this.gameState.Current;
		if (!state.FrameworkTick)
		{
			this.frameworkDispatcher.TryRun(this.gameState.Update, out _);
			state = this.gameState.Current;
		}

		if (path.EndsWith("/gpose/prepare-posing", StringComparison.OrdinalIgnoreCase)
			&& method == "POST")
		{
			PrepareForPosingResult? prepareResult = null;
			this.frameworkDispatcher.TryRun(
				() => prepareResult = this.gposeControllerService.PrepareForPosing(),
				out string? prepareError);
			if (prepareResult == null)
			{
				this.WriteJson(stream, 503, new GposePrepareResponse
				{
					Ok = false,
					Error = prepareError ?? "Framework thread unavailable.",
				});
				return;
			}

			GposeCameraState cameras = this.gposeControllerService.Snapshot();
			this.WriteJson(stream, 200, new GposePrepareResponse
			{
				Ok = prepareResult.Value.Ok,
				DisabledFaceCamera = prepareResult.Value.DisabledFaceCamera,
				DisabledGazeCamera = prepareResult.Value.DisabledGazeCamera,
				FaceCameraEnabled = cameras.FaceCameraEnabled,
				GazeCameraEnabled = cameras.GazeCameraEnabled,
				Error = prepareResult.Value.Error,
			});
			return;
		}

		if (path.EndsWith("/gpose", StringComparison.OrdinalIgnoreCase))
		{
			GposeCameraState cameraState = default;
			this.frameworkDispatcher.TryRun(
				() => cameraState = this.gposeControllerService.Snapshot(),
				out _);
			this.WriteJson(stream, 200, new GposeResponse
			{
				IsInGpose = state.IsInGpose,
				FaceCameraEnabled = cameraState.FaceCameraEnabled,
				GazeCameraEnabled = cameraState.GazeCameraEnabled,
			});
			return;
		}

		if (path.EndsWith("/camera", StringComparison.OrdinalIgnoreCase))
		{
			if (method == "GET")
			{
				CameraSnapshot snapshot = default;
				this.frameworkDispatcher.TryRun(
					() => snapshot = this.cameraService.Read(),
					out string? readError);
				if (readError != null && !snapshot.Available)
				{
					this.WriteJson(stream, 503, new CameraResponse
					{
						Ok = false,
						Error = readError,
					});
					return;
				}

				this.WriteJson(stream, 200, ToCameraResponse(snapshot));
				return;
			}

			if (method == "POST")
			{
				CameraUpdateRequest? request = DeserializeBody<CameraUpdateRequest>(body);
				if (request == null)
				{
					this.WriteJson(stream, 400, new CameraResponse { Ok = false, Error = "Invalid JSON body." });
					return;
				}

				(bool ok, string? error) applyResult = default;
				CameraSnapshot after = default;
				this.frameworkDispatcher.TryRun(
					() =>
					{
						applyResult = this.cameraService.Apply(ToCameraUpdate(request));
						after = this.cameraService.Read();
					},
					out string? applyThreadError);

				if (applyThreadError != null)
				{
					this.WriteJson(stream, 503, new CameraResponse { Ok = false, Error = applyThreadError });
					return;
				}

				if (!applyResult.ok)
				{
					this.WriteJson(stream, 400, new CameraResponse { Ok = false, Error = applyResult.error });
					return;
				}

				this.WriteJson(stream, 200, ToCameraResponse(after, ok: true));
				return;
			}
		}

		if (path.EndsWith("/camera/shot", StringComparison.OrdinalIgnoreCase))
		{
			if (method == "GET")
			{
				string? indexText = GetQuery(queryString, "objectIndex");
				if (!int.TryParse(indexText, out int objectIndex))
				{
					this.WriteJson(stream, 400, new CameraShotResponse { Ok = false, Error = "objectIndex query required." });
					return;
				}

				(bool ok, CameraShotData? shot, string? error) exportResult = default;
				this.frameworkDispatcher.TryRun(
					() => exportResult = this.cameraService.ExportShot(objectIndex),
					out string? exportThreadError);

				if (exportThreadError != null)
				{
					this.WriteJson(stream, 503, new CameraShotResponse { Ok = false, Error = exportThreadError });
					return;
				}

				if (!exportResult.ok || exportResult.shot == null)
				{
					this.WriteJson(stream, 400, new CameraShotResponse { Ok = false, Error = exportResult.error });
					return;
				}

				this.WriteJson(stream, 200, new CameraShotResponse
				{
					Ok = true,
					ObjectIndex = objectIndex,
					Shot = ToCameraShotDto(exportResult.shot.Value),
				});
				return;
			}

			if (method == "POST")
			{
				CameraShotApplyRequest? request = DeserializeBody<CameraShotApplyRequest>(body);
				if (request?.Shot == null)
				{
					this.WriteJson(stream, 400, new CameraShotResponse { Ok = false, Error = "Invalid JSON body." });
					return;
				}

				(bool ok, string? error) applyResult = default;
				this.frameworkDispatcher.TryRun(
					() => applyResult = this.cameraService.ApplyShot(request.ObjectIndex, ToCameraShotData(request.Shot)),
					out string? applyThreadError);

				if (applyThreadError != null)
				{
					this.WriteJson(stream, 503, new CameraShotResponse { Ok = false, Error = applyThreadError });
					return;
				}

				if (!applyResult.ok)
				{
					this.WriteJson(stream, 400, new CameraShotResponse { Ok = false, Error = applyResult.error });
					return;
				}

				this.WriteJson(stream, 200, new CameraShotResponse { Ok = true, ObjectIndex = request.ObjectIndex });
				return;
			}
		}

		if (path.EndsWith("/world", StringComparison.OrdinalIgnoreCase))
		{
			if (method == "GET")
			{
				WorldSnapshot snapshot = default;
				this.frameworkDispatcher.TryRun(
					() => snapshot = this.worldService.Read(state.IsInGpose),
					out string? readError);
				if (readError != null && !snapshot.Available)
				{
					this.WriteJson(stream, 503, new WorldResponse
					{
						Ok = false,
						Error = readError,
					});
					return;
				}

				this.WriteJson(stream, 200, ToWorldResponse(snapshot));
				return;
			}

			if (method == "POST")
			{
				WorldUpdateRequest? request = DeserializeBody<WorldUpdateRequest>(body);
				if (request == null)
				{
					this.WriteJson(stream, 400, new WorldResponse { Ok = false, Error = "Invalid JSON body." });
					return;
				}

				(bool ok, string? error) applyResult = default;
				WorldSnapshot after = default;
				this.frameworkDispatcher.TryRun(
					() =>
					{
						applyResult = this.worldService.Apply(state.IsInGpose, ToWorldUpdate(request));
						after = this.worldService.Read(state.IsInGpose);
					},
					out string? applyThreadError);

				if (applyThreadError != null)
				{
					this.WriteJson(stream, 503, new WorldResponse { Ok = false, Error = applyThreadError });
					return;
				}

				if (!applyResult.ok)
				{
					this.WriteJson(stream, 400, new WorldResponse { Ok = false, Error = applyResult.error });
					return;
				}

				this.WriteJson(stream, 200, ToWorldResponse(after, ok: true));
				return;
			}
		}

		if (path.EndsWith("/territory", StringComparison.OrdinalIgnoreCase))
		{
			this.WriteJson(stream, 200, new TerritoryResponse
			{
				TerritoryId = state.TerritoryId,
				SignedIn = state.SignedIn,
			});
			return;
		}

		if (path.EndsWith("/capabilities", StringComparison.OrdinalIgnoreCase) && method == "GET")
		{
			this.WriteJson(stream, 200, new CapabilitiesResponse
			{
				Ok = true,
				Version = BridgeVersion.Current,
				Step = "camera-animation-world",
				Api = 6,
				Capabilities =
				[
					"health",
					"gpose",
					"camera.read",
					"camera.write",
					"camera.shot",
					"world.read",
					"world.write",
					"territory",
					"actors",
					"actors.motion.read",
					"actors.motion.write",
					"actors.animation.read",
					"actors.animation.write",
					"target",
					"appearance.read",
					"appearance.write.fullCustomize",
					"appearance.redraw",
					"equipment.read",
					"equipment.write",
					"gameData.items",
					"gameData.dyes",
					"gameData.weathers",
					"gameData.emotes",
					"gameData.colors",
					"gameData.customizeOptions",
					"skeleton.read",
					"skeleton.write",
					"skeleton.applyPose",
					"ipc.posingFlags",
					"ipc.posingHooks",
				],
			});
			return;
		}

		if (path.EndsWith("/game-data/dyes", StringComparison.OrdinalIgnoreCase) && method == "GET")
		{
			this.WriteJson(stream, 200, new DyesResponse { Dyes = this.gameDataService.GetDyes() });
			return;
		}

		if (path.EndsWith("/game-data/weathers", StringComparison.OrdinalIgnoreCase) && method == "GET")
		{
			this.WriteJson(stream, 200, new WeathersResponse { Weathers = this.gameDataService.GetWeathers() });
			return;
		}

		if (path.EndsWith("/game-data/emotes", StringComparison.OrdinalIgnoreCase) && method == "GET")
		{
			this.WriteJson(stream, 200, new EmotesResponse { Emotes = this.gameDataService.GetEmotes() });
			return;
		}

		if (path.EndsWith("/game-data/items", StringComparison.OrdinalIgnoreCase) && method == "GET")
		{
			string slot = GetQuery(queryString, "slot") ?? "body";
			this.WriteJson(stream, 200, new CatalogItemsResponse
			{
				Slot = slot,
				Items = this.gameDataService.GetItemsForSlot(slot),
			});
			return;
		}

		if (path.EndsWith("/game-data/colors", StringComparison.OrdinalIgnoreCase) && method == "GET")
		{
			string type = GetQuery(queryString, "type") ?? "hair";
			byte tribe = byte.TryParse(GetQuery(queryString, "tribe"), out byte t) ? t : (byte)1;
			byte gender = byte.TryParse(GetQuery(queryString, "gender"), out byte g) ? g : (byte)0;
			this.WriteJson(stream, 200, new ColorsResponse
			{
				Type = type,
				Colors = this.gameDataService.GetColors(type, tribe, gender),
			});
			return;
		}

		if (path.EndsWith("/game-data/customize-options", StringComparison.OrdinalIgnoreCase) && method == "GET")
		{
			string kind = GetQuery(queryString, "kind") ?? "hair";
			byte tribe = byte.TryParse(GetQuery(queryString, "tribe"), out byte t) ? t : (byte)1;
			byte gender = byte.TryParse(GetQuery(queryString, "gender"), out byte g) ? g : (byte)0;
			byte face = byte.TryParse(GetQuery(queryString, "face"), out byte f) ? f : (byte)1;
			byte age = byte.TryParse(GetQuery(queryString, "age"), out byte a) ? a : (byte)1;
			this.WriteJson(stream, 200, new CustomizeOptionsResponse
			{
				Kind = kind,
				Options = this.customizeOptionsService.GetOptions(kind, tribe, gender, face, age),
			});
			return;
		}

		if (path.Contains("/game-data/icon/", StringComparison.OrdinalIgnoreCase) && method == "GET")
		{
			int iconIndex = path.LastIndexOf("/game-data/icon/", StringComparison.OrdinalIgnoreCase);
			string iconToken = path[(iconIndex + "/game-data/icon/".Length)..];
			if (uint.TryParse(iconToken, out uint iconId))
			{
				byte[]? bmp = this.gameDataService.TryGetIconBmp(iconId);
				if (bmp != null)
				{
					this.WriteBytes(stream, 200, "image/bmp", bmp);
					return;
				}
			}

			this.WriteJson(stream, 404, new ErrorResponse { Ok = false, Error = "Icon not found." });
			return;
		}

		if (path.EndsWith("/actors", StringComparison.OrdinalIgnoreCase) && method == "GET")
		{
			this.WriteJson(stream, 200, new ActorsResponse { Actors = this.actors.Current });
			return;
		}

		if (path.EndsWith("/local-player", StringComparison.OrdinalIgnoreCase))
		{
			BridgeActorDto? actor = this.runtimeCache.LocalPlayer;
			if (actor == null)
			{
				this.WriteJson(stream, 404, new ErrorResponse { Ok = false, Error = "Local player not available." });
				return;
			}

			this.WriteJson(stream, 200, actor);
			return;
		}

		if (path.EndsWith("/target", StringComparison.OrdinalIgnoreCase))
		{
			if (method == "GET")
			{
				BridgeActorDto? actor = this.runtimeCache.CurrentTarget;
				if (actor == null)
				{
					this.WriteJson(stream, 404, new ErrorResponse { Ok = false, Error = "No target." });
					return;
				}

				this.WriteJson(stream, 200, actor);
				return;
			}

			if (method == "POST")
			{
				TargetSetRequest? request = DeserializeBody<TargetSetRequest>(body);
				if (request == null)
				{
					this.WriteJson(stream, 400, new TargetSetResponse { Ok = false, Error = "Invalid request body." });
					return;
				}

				bool ok = false;
				string? targetError = null;
				if (!this.frameworkDispatcher.TryRun(
					() => ok = this.targetService.TrySetTarget(request.ObjectIndex, out targetError),
					out string? dispatchError))
				{
					this.WriteJson(stream, 503, new TargetSetResponse { Ok = false, Error = dispatchError ?? "Framework dispatch failed." });
					return;
				}

				this.WriteJson(stream, ok ? 200 : 400, new TargetSetResponse { Ok = ok, Error = targetError });
				return;
			}
		}

		if (path.Contains("/actors/", StringComparison.OrdinalIgnoreCase)
			&& path.EndsWith("/appearance", StringComparison.OrdinalIgnoreCase))
		{
			int actorsIndex = path.IndexOf("/actors/", StringComparison.OrdinalIgnoreCase);
			int appearanceIndex = path.LastIndexOf("/appearance", StringComparison.OrdinalIgnoreCase);
			if (actorsIndex >= 0 && appearanceIndex > actorsIndex
				&& int.TryParse(path[(actorsIndex + "/actors/".Length)..appearanceIndex], out int objectIndex))
			{
				if (method == "POST")
				{
					ActorAppearanceDto? request = DeserializeBody<ActorAppearanceDto>(body);
					if (request == null)
					{
						this.WriteJson(stream, 400, new AppearanceSetResponse { Ok = false, Error = "Invalid request body." });
						return;
					}

					bool ok = false;
					string? applyError = null;
					if (!this.frameworkDispatcher.TryRun(
						() =>
						{
							ok = this.appearanceService.TryApplyAppearance(objectIndex, request, out applyError);
							if (ok)
							{
								this.runtimeCache.RefreshAppearance(this.appearanceService, objectIndex);
							}
						},
						out string? dispatchError))
					{
						this.WriteJson(stream, 503, new AppearanceSetResponse { Ok = false, Error = dispatchError ?? "Framework dispatch failed." });
						return;
					}

					this.WriteJson(stream, ok ? 200 : 400, new AppearanceSetResponse { Ok = ok, Error = applyError });
					return;
				}

				ActorAppearanceDto? appearance = this.runtimeCache.GetAppearance(objectIndex);
				if (appearance == null)
				{
					if (!this.frameworkDispatcher.TryRun(
						() =>
						{
							this.runtimeCache.RefreshAppearance(this.appearanceService, objectIndex);
							appearance = this.runtimeCache.GetAppearance(objectIndex);
						},
						out _))
					{
						this.WriteJson(stream, 503, new ErrorResponse { Ok = false, Error = "Framework dispatch failed." });
						return;
					}
				}

				if (appearance == null)
				{
					this.WriteJson(stream, 404, new ErrorResponse { Ok = false, Error = "Actor appearance not available." });
					return;
				}

				this.WriteJson(stream, 200, appearance);
				return;
			}
		}

		if (path.Contains("/actors/", StringComparison.OrdinalIgnoreCase)
			&& path.EndsWith("/equipment", StringComparison.OrdinalIgnoreCase))
		{
			int actorsIndex = path.IndexOf("/actors/", StringComparison.OrdinalIgnoreCase);
			int equipmentIndex = path.LastIndexOf("/equipment", StringComparison.OrdinalIgnoreCase);
			if (actorsIndex >= 0 && equipmentIndex > actorsIndex
				&& int.TryParse(path[(actorsIndex + "/actors/".Length)..equipmentIndex], out int objectIndex))
			{
				if (method == "POST")
				{
					ActorEquipmentDto? request = DeserializeBody<ActorEquipmentDto>(body);
					if (request == null)
					{
						this.WriteJson(stream, 400, new EquipmentSetResponse { Ok = false, Error = "Invalid request body." });
						return;
					}

					bool ok = false;
					string? equipError = null;
					if (!this.frameworkDispatcher.TryRun(
						() => ok = this.equipmentService.TryApplyEquipment(objectIndex, request, out equipError),
						out string? dispatchError))
					{
						this.WriteJson(stream, 503, new EquipmentSetResponse { Ok = false, Error = dispatchError ?? "Framework dispatch failed." });
						return;
					}

					this.WriteJson(stream, ok ? 200 : 400, new EquipmentSetResponse { Ok = ok, Error = equipError });
					return;
				}

				ActorEquipmentDto? equipment = null;
				if (!this.frameworkDispatcher.TryRun(
					() => equipment = this.equipmentService.TryGetEquipment(objectIndex),
					out string? getDispatchError))
				{
					this.WriteJson(stream, 503, new ErrorResponse { Ok = false, Error = getDispatchError ?? "Framework dispatch failed." });
					return;
				}

				if (equipment == null)
				{
					this.WriteJson(stream, 404, new ErrorResponse { Ok = false, Error = "Equipment not available." });
					return;
				}

				this.WriteJson(stream, 200, equipment);
				return;
			}
		}

		if (path.Contains("/actors/", StringComparison.OrdinalIgnoreCase)
			&& path.EndsWith("/skeleton/apply-pose", StringComparison.OrdinalIgnoreCase)
			&& method == "POST")
		{
			int actorsIndex = path.IndexOf("/actors/", StringComparison.OrdinalIgnoreCase);
			int applyIndex = path.LastIndexOf("/skeleton/apply-pose", StringComparison.OrdinalIgnoreCase);
			if (actorsIndex >= 0 && applyIndex > actorsIndex
				&& int.TryParse(path[(actorsIndex + "/actors/".Length)..applyIndex], out int objectIndex))
			{
				ApplyPoseRequest? request = DeserializeBody<ApplyPoseRequest>(body);
				if (request == null)
				{
					this.WriteJson(stream, 400, new ApplyPoseResponse
					{
						Ok = false,
						ObjectIndex = objectIndex,
						Error = "Invalid request body.",
					});
					return;
				}

				ApplyPoseResponse? response = null;
				if (!this.frameworkDispatcher.TryRun(
					() =>
					{
						this.ipcService.MarkPosingUsed();
						response = this.skeletonService.TryApplyPose(objectIndex, request);
					},
					out string? dispatchError))
				{
					this.WriteJson(stream, 503, new ApplyPoseResponse
					{
						Ok = false,
						ObjectIndex = objectIndex,
						Error = dispatchError ?? "Framework dispatch failed.",
					});
					return;
				}

				this.WriteJson(stream, response?.Ok == true ? 200 : 400, response ?? new ApplyPoseResponse
				{
					Ok = false,
					ObjectIndex = objectIndex,
					Error = "Pose apply failed.",
				});
				return;
			}
		}

		if (path.Contains("/actors/", StringComparison.OrdinalIgnoreCase)
			&& path.EndsWith("/skeleton", StringComparison.OrdinalIgnoreCase))
		{
			int actorsIndex = path.IndexOf("/actors/", StringComparison.OrdinalIgnoreCase);
			int skeletonIndex = path.LastIndexOf("/skeleton", StringComparison.OrdinalIgnoreCase);
			if (actorsIndex >= 0 && skeletonIndex > actorsIndex
				&& int.TryParse(path[(actorsIndex + "/actors/".Length)..skeletonIndex], out int objectIndex))
			{
				if (method == "GET")
				{
					SkeletonResponse? skeleton = null;
					if (!this.frameworkDispatcher.TryRun(
						() => skeleton = this.skeletonService.TryGetSkeleton(objectIndex),
						out string? dispatchError))
					{
						this.WriteJson(stream, 503, new SkeletonResponse
						{
							Ok = false,
							ObjectIndex = objectIndex,
							Error = dispatchError ?? "Framework dispatch failed.",
						});
						return;
					}

					this.WriteJson(stream, skeleton?.Ok == true ? 200 : 400, skeleton ?? new SkeletonResponse
					{
						Ok = false,
						ObjectIndex = objectIndex,
						Error = "Skeleton read failed.",
					});
					return;
				}

				if (method == "POST")
				{
					SetBoneTransformRequest? request = DeserializeBody<SetBoneTransformRequest>(body);
					if (request == null)
					{
						this.WriteJson(stream, 400, new SetBoneTransformResponse
						{
							Ok = false,
							ObjectIndex = objectIndex,
							Error = "Invalid request body.",
						});
						return;
					}

					SetBoneTransformResponse? response = null;
					if (!this.frameworkDispatcher.TryRun(
						() => response = this.skeletonService.TrySetBoneTransform(objectIndex, request),
						out string? dispatchError))
					{
						this.WriteJson(stream, 503, new SetBoneTransformResponse
						{
							Ok = false,
							ObjectIndex = objectIndex,
							Error = dispatchError ?? "Framework dispatch failed.",
						});
						return;
					}

					this.WriteJson(stream, response?.Ok == true ? 200 : 400, response ?? new SetBoneTransformResponse
					{
						Ok = false,
						ObjectIndex = objectIndex,
						Error = "Bone write failed.",
					});
					return;
				}
			}
		}

		if (path.Contains("/actors/", StringComparison.OrdinalIgnoreCase)
			&& path.EndsWith("/motion", StringComparison.OrdinalIgnoreCase))
		{
			int actorsIndex = path.IndexOf("/actors/", StringComparison.OrdinalIgnoreCase);
			int motionIndex = path.LastIndexOf("/motion", StringComparison.OrdinalIgnoreCase);
			if (actorsIndex >= 0 && motionIndex > actorsIndex
				&& int.TryParse(path[(actorsIndex + "/actors/".Length)..motionIndex], out int objectIndex))
			{
				if (method == "POST")
				{
					MotionUpdateRequest? request = DeserializeBody<MotionUpdateRequest>(body);
					if (request == null)
					{
						this.WriteJson(stream, 400, new MotionResponse { Ok = false, ObjectIndex = objectIndex, Error = "Invalid request body." });
						return;
					}

					MotionResponse? response = null;
					if (!this.frameworkDispatcher.TryRun(
						() => response = this.motionService.TrySetMotion(objectIndex, request),
						out string? dispatchError))
					{
						this.WriteJson(stream, 503, new MotionResponse
						{
							Ok = false,
							ObjectIndex = objectIndex,
							Error = dispatchError ?? "Framework dispatch failed.",
						});
						return;
					}

					this.WriteJson(stream, response?.Ok == true ? 200 : 400, response ?? new MotionResponse
					{
						Ok = false,
						ObjectIndex = objectIndex,
						Error = "Motion write failed.",
					});
					return;
				}

				if (method == "GET")
				{
					MotionResponse? response = null;
					if (!this.frameworkDispatcher.TryRun(
						() => response = this.motionService.TryGetMotion(objectIndex),
						out string? dispatchError))
					{
						this.WriteJson(stream, 503, new MotionResponse
						{
							Ok = false,
							ObjectIndex = objectIndex,
							Error = dispatchError ?? "Framework dispatch failed.",
						});
						return;
					}

					this.WriteJson(stream, response?.Ok == true ? 200 : 404, response ?? new MotionResponse
					{
						Ok = false,
						ObjectIndex = objectIndex,
						Error = "Motion read failed.",
					});
					return;
				}
			}
		}

		if (path.Contains("/actors/", StringComparison.OrdinalIgnoreCase)
			&& path.EndsWith("/animation", StringComparison.OrdinalIgnoreCase))
		{
			int actorsIndex = path.IndexOf("/actors/", StringComparison.OrdinalIgnoreCase);
			int animationIndex = path.LastIndexOf("/animation", StringComparison.OrdinalIgnoreCase);
			if (actorsIndex >= 0 && animationIndex > actorsIndex
				&& int.TryParse(path[(actorsIndex + "/actors/".Length)..animationIndex], out int objectIndex))
			{
				if (method == "POST")
				{
					AnimationUpdateRequest? request = DeserializeBody<AnimationUpdateRequest>(body);
					if (request == null)
					{
						this.WriteJson(stream, 400, new AnimationResponse { Ok = false, ObjectIndex = objectIndex, Error = "Invalid request body." });
						return;
					}

					AnimationResponse? response = null;
					if (!this.frameworkDispatcher.TryRun(
						() => response = this.animationService.TrySetAnimation(objectIndex, request),
						out string? dispatchError))
					{
						this.WriteJson(stream, 503, new AnimationResponse
						{
							Ok = false,
							ObjectIndex = objectIndex,
							Error = dispatchError ?? "Framework dispatch failed.",
						});
						return;
					}

					this.WriteJson(stream, response?.Ok == true ? 200 : 400, response ?? new AnimationResponse
					{
						Ok = false,
						ObjectIndex = objectIndex,
						Error = "Animation write failed.",
					});
					return;
				}

				if (method == "GET")
				{
					AnimationResponse? response = null;
					if (!this.frameworkDispatcher.TryRun(
						() => response = this.animationService.TryGetAnimation(objectIndex),
						out string? dispatchError))
					{
						this.WriteJson(stream, 503, new AnimationResponse
						{
							Ok = false,
							ObjectIndex = objectIndex,
							Error = dispatchError ?? "Framework dispatch failed.",
						});
						return;
					}

					this.WriteJson(stream, response?.Ok == true ? 200 : 404, response ?? new AnimationResponse
					{
						Ok = false,
						ObjectIndex = objectIndex,
						Error = "Animation read failed.",
					});
					return;
				}
			}
		}

		if (path.Contains("/actors/", StringComparison.OrdinalIgnoreCase)
			&& path.EndsWith("/redraw", StringComparison.OrdinalIgnoreCase)
			&& method == "POST")
		{
			int actorsIndex = path.IndexOf("/actors/", StringComparison.OrdinalIgnoreCase);
			int redrawIndex = path.LastIndexOf("/redraw", StringComparison.OrdinalIgnoreCase);
			if (actorsIndex >= 0 && redrawIndex > actorsIndex
				&& int.TryParse(path[(actorsIndex + "/actors/".Length)..redrawIndex], out int objectIndex))
			{
				bool ok = false;
				string? redrawError = null;
				if (!this.frameworkDispatcher.TryRun(
					() => ok = this.redrawService.TryRedraw(objectIndex, out redrawError),
					out string? dispatchError))
				{
					this.WriteJson(stream, 503, new RedrawResponse { Ok = false, Error = dispatchError ?? "Framework dispatch failed." });
					return;
				}

				this.WriteJson(stream, ok ? 200 : 400, new RedrawResponse { Ok = ok, Error = redrawError });
				return;
			}
		}

		if (path.EndsWith("/status", StringComparison.OrdinalIgnoreCase))
		{
			this.WriteJson(stream, 200, new StatusResponse
			{
				IsInGpose = state.IsInGpose,
				TerritoryId = state.TerritoryId,
				SignedIn = state.SignedIn,
				FrameworkTick = state.FrameworkTick,
			});
			return;
		}

		if (path.EndsWith("/ipc", StringComparison.OrdinalIgnoreCase) && method == "GET")
		{
			this.WriteJson(stream, 200, this.ipcService.Snapshot());
			return;
		}

		if (path.EndsWith("/ipc", StringComparison.OrdinalIgnoreCase) && method == "POST")
		{
			BridgeIpcCommandRequest? request = DeserializeBody<BridgeIpcCommandRequest>(body);
			if (request == null)
			{
				this.WriteJson(stream, 400, new BridgeIpcCommandResponse { Ok = false, Error = "Invalid request body." });
				return;
			}

			BridgeIpcCommandResponse response = default!;
			if (!this.frameworkDispatcher.TryRun(
				() => response = this.ipcService.Execute(request),
				out string? dispatchError))
			{
				this.WriteJson(stream, 503, new BridgeIpcCommandResponse { Ok = false, Error = dispatchError ?? "Framework dispatch failed." });
				return;
			}

			this.WriteJson(stream, response.Ok ? 200 : 400, response);
			return;
		}

		this.WriteJson(stream, 404, new ErrorResponse { Ok = false, Error = "Not found." });
	}

	private static bool TryReadHttpRequest(Stream stream, out string method, out string path, out string body)
	{
		method = string.Empty;
		path = string.Empty;
		body = string.Empty;

		using var reader = new StreamReader(stream, Encoding.ASCII, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
		string? requestLine = reader.ReadLine();
		if (string.IsNullOrWhiteSpace(requestLine))
		{
			return false;
		}

		string[] parts = requestLine.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
		if (parts.Length < 2)
		{
			return false;
		}

		method = parts[0].ToUpperInvariant();
		path = parts[1];

		int contentLength = 0;
		while (true)
		{
			string? header = reader.ReadLine();
			if (header == null || header.Length == 0)
			{
				break;
			}

			if (header.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)
				&& int.TryParse(header["Content-Length:".Length..].Trim(), out int length))
			{
				contentLength = Math.Clamp(length, 0, 1_000_000);
			}
		}

		if (contentLength > 0)
		{
			char[] buffer = new char[contentLength];
			int read = 0;
			while (read < contentLength)
			{
				int n = reader.Read(buffer, read, contentLength - read);
				if (n <= 0)
				{
					break;
				}

				read += n;
			}

			body = new string(buffer, 0, read);
		}

		return true;
	}

	private static T? DeserializeBody<T>(string body)
	{
		if (string.IsNullOrWhiteSpace(body))
		{
			return default;
		}

		return JsonSerializer.Deserialize<T>(body, JsonOptions);
	}

	private static string? GetQuery(string queryString, string key)
	{
		if (string.IsNullOrEmpty(queryString))
		{
			return null;
		}

		foreach (string part in queryString.Split('&', StringSplitOptions.RemoveEmptyEntries))
		{
			int eq = part.IndexOf('=');
			if (eq <= 0)
			{
				continue;
			}

			string name = Uri.UnescapeDataString(part[..eq]);
			if (!string.Equals(name, key, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			return Uri.UnescapeDataString(part[(eq + 1)..]);
		}

		return null;
	}

	private void WriteJson(Stream stream, int statusCode, object payload)
	{
		byte[] json = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, JsonOptions));
		this.WriteBytes(stream, statusCode, "application/json; charset=utf-8", json);
	}

	private void WriteBytes(Stream stream, int statusCode, string contentType, byte[] payload)
	{
		string reason = statusCode switch
		{
			200 => "OK",
			400 => "Bad Request",
			404 => "Not Found",
			500 => "Internal Server Error",
			503 => "Service Unavailable",
			_ => "OK",
		};

		string header =
			$"HTTP/1.1 {statusCode} {reason}\r\n" +
			$"Content-Type: {contentType}\r\n" +
			$"Content-Length: {payload.Length}\r\n" +
			"Connection: close\r\n\r\n";

		byte[] headerBytes = Encoding.ASCII.GetBytes(header);
		stream.Write(headerBytes, 0, headerBytes.Length);
		stream.Write(payload, 0, payload.Length);
		stream.Flush();
	}

	private static WorldResponse ToWorldResponse(WorldSnapshot snapshot, bool ok = false)
	{
		return new WorldResponse
		{
			Ok = ok || snapshot.Available,
			Available = snapshot.Available,
			IsInGpose = snapshot.IsInGpose,
			TimeOfDayMinutes = snapshot.TimeOfDayMinutes,
			TimeString = snapshot.TimeString,
			DayOfMonth = snapshot.DayOfMonth,
			WeatherId = snapshot.WeatherId,
			WeatherName = snapshot.WeatherName,
			WeatherIconId = snapshot.WeatherIconId,
			FreezeTime = snapshot.FreezeTime,
			HoldWeather = snapshot.HoldWeather,
			Error = snapshot.Error,
		};
	}

	private static WorldUpdate ToWorldUpdate(WorldUpdateRequest request)
	{
		return new WorldUpdate(
			request.TimeOfDayMinutes,
			request.DayOfMonth,
			request.WeatherId,
			request.FreezeTime,
			request.HoldWeather);
	}

	private static CameraResponse ToCameraResponse(CameraSnapshot snapshot, bool ok = false)
	{
		return new CameraResponse
		{
			Ok = ok || snapshot.Available,
			Available = snapshot.Available,
			IsInGpose = snapshot.IsInGpose,
			DelimitCamera = snapshot.DelimitCamera,
			Zoom = snapshot.Zoom,
			MinZoom = snapshot.MinZoom,
			MaxZoom = snapshot.MaxZoom,
			FieldOfView = snapshot.FieldOfView,
			AngleXDeg = snapshot.AngleXDeg,
			AngleYDeg = snapshot.AngleYDeg,
			RotationDeg = snapshot.RotationDeg,
			PanXDeg = snapshot.PanXDeg,
			PanYDeg = snapshot.PanYDeg,
			PositionX = snapshot.PositionX,
			PositionY = snapshot.PositionY,
			PositionZ = snapshot.PositionZ,
			Error = snapshot.Error,
		};
	}

	private static CameraUpdate ToCameraUpdate(CameraUpdateRequest request)
	{
		return new CameraUpdate(
			request.DelimitCamera,
			request.Zoom,
			request.FieldOfView,
			request.AngleXDeg,
			request.AngleYDeg,
			request.RotationDeg,
			request.PanXDeg,
			request.PanYDeg,
			request.PositionX,
			request.PositionY,
			request.PositionZ);
	}

	private static CameraShotDto ToCameraShotDto(CameraShotData shot)
	{
		return new CameraShotDto
		{
			DelimitCamera = shot.DelimitCamera,
			Zoom = shot.Zoom,
			FieldOfView = shot.FieldOfView,
			PanX = shot.Pan.X,
			PanY = shot.Pan.Y,
			PositionX = shot.Position.X,
			PositionY = shot.Position.Y,
			PositionZ = shot.Position.Z,
			RotationX = shot.Rotation.X,
			RotationY = shot.Rotation.Y,
			RotationZ = shot.Rotation.Z,
		};
	}

	private static CameraShotData ToCameraShotData(CameraShotDto shot)
	{
		return new CameraShotData(
			shot.DelimitCamera,
			shot.Zoom,
			shot.FieldOfView,
			new System.Numerics.Vector2(shot.PanX, shot.PanY),
			new System.Numerics.Vector3(shot.PositionX, shot.PositionY, shot.PositionZ),
			new System.Numerics.Vector3(shot.RotationX, shot.RotationY, shot.RotationZ));
	}
}
