// © Anamnesis.
// Licensed under the MIT license.

namespace AnamnesisBridge.Services;

using AnamnesisBridge.Api;
using Dalamud.Plugin.Services;
using System;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Localhost HTTP server for native Linux Anamnesis status polling.
/// </summary>
public sealed class BridgeHttpServer : IDisposable
{
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		WriteIndented = false,
	};

	private readonly IPluginLog log;
	private readonly GameStateService gameState;
	private readonly ActorEnumerationService actors;
	private readonly Func<Configuration> getConfiguration;

	private HttpListener? listener;
	private CancellationTokenSource? cancellation;
	private Task? listenTask;

	public BridgeHttpServer(
		IPluginLog log,
		GameStateService gameState,
		ActorEnumerationService actors,
		Func<Configuration> getConfiguration)
	{
		this.log = log;
		this.gameState = gameState;
		this.actors = actors;
		this.getConfiguration = getConfiguration;
	}

	public bool IsRunning => this.listener?.IsListening == true;

	public void Restart()
	{
		this.Stop();
		Configuration config = this.getConfiguration();
		if (!config.Enabled)
		{
			return;
		}

		string prefix = $"http://{config.BindAddress}:{config.Port}/anamnesis/v1/";
		this.listener = new HttpListener();
		this.listener.Prefixes.Add(prefix);
		this.listener.Start();

		this.cancellation = new CancellationTokenSource();
		this.listenTask = Task.Run(() => this.ListenLoopAsync(this.cancellation.Token));

		this.log.Information($"AnamnesisBridge listening on {prefix}");
	}

	public void Stop()
	{
		try
		{
			this.cancellation?.Cancel();
		}
		catch
		{
			// Best effort shutdown only.
		}

		if (this.listener?.IsListening == true)
		{
			this.listener.Stop();
		}

		this.listener?.Close();
		this.listener = null;
		this.cancellation?.Dispose();
		this.cancellation = null;
	}

	public void Dispose() => this.Stop();

	private async Task ListenLoopAsync(CancellationToken cancellationToken)
	{
		while (!cancellationToken.IsCancellationRequested && this.listener?.IsListening == true)
		{
			try
			{
				HttpListenerContext context = await this.listener.GetContextAsync().WaitAsync(cancellationToken);
				_ = Task.Run(() => this.HandleRequest(context), cancellationToken);
			}
			catch (OperationCanceledException)
			{
				break;
			}
			catch (Exception ex)
			{
				if (!cancellationToken.IsCancellationRequested)
				{
					this.log.Warning(ex, "AnamnesisBridge HTTP listener error.");
				}
			}
		}
	}

	private void HandleRequest(HttpListenerContext context)
	{
		try
		{
			string path = context.Request.Url?.AbsolutePath.TrimEnd('/') ?? string.Empty;
			if (path.EndsWith("/health", StringComparison.OrdinalIgnoreCase))
			{
				this.WriteJson(context, 200, new HealthResponse());
				return;
			}

			GameStateSnapshot state = this.gameState.Current;
			if (!state.FrameworkTick)
			{
				this.WriteJson(context, 503, new ErrorResponse { Ok = false, Error = "Framework not ready." });
				return;
			}

			if (path.EndsWith("/gpose", StringComparison.OrdinalIgnoreCase))
			{
				this.WriteJson(context, 200, new GposeResponse { IsInGpose = state.IsInGpose });
				return;
			}

			if (path.EndsWith("/territory", StringComparison.OrdinalIgnoreCase))
			{
				this.WriteJson(context, 200, new TerritoryResponse
				{
					TerritoryId = state.TerritoryId,
					SignedIn = state.SignedIn,
				});
				return;
			}

			if (path.EndsWith("/actors", StringComparison.OrdinalIgnoreCase))
			{
				this.WriteJson(context, 200, new ActorsResponse { Actors = this.actors.Current });
				return;
			}

			if (path.EndsWith("/status", StringComparison.OrdinalIgnoreCase))
			{
				this.WriteJson(context, 200, new StatusResponse
				{
					IsInGpose = state.IsInGpose,
					TerritoryId = state.TerritoryId,
					SignedIn = state.SignedIn,
					FrameworkTick = state.FrameworkTick,
				});
				return;
			}

			this.WriteJson(context, 404, new ErrorResponse { Ok = false, Error = "Not found." });
		}
		catch (Exception ex)
		{
			this.log.Warning(ex, "AnamnesisBridge request failed.");
			try
			{
				this.WriteJson(context, 500, new ErrorResponse { Ok = false, Error = ex.Message });
			}
			catch
			{
				// Ignore secondary failures while handling errors.
			}
		}
	}

	private void WriteJson(HttpListenerContext context, int statusCode, object payload)
	{
		byte[] body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, JsonOptions));
		context.Response.StatusCode = statusCode;
		context.Response.ContentType = "application/json";
		context.Response.ContentEncoding = Encoding.UTF8;
		context.Response.ContentLength64 = body.Length;
		context.Response.OutputStream.Write(body, 0, body.Length);
		context.Response.OutputStream.Close();
	}
}
