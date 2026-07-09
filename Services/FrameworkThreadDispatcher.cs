// © DeadlyDeathAngel.
// Licensed under the MIT license.

namespace LuminusBridge.Services;

using Dalamud.Plugin.Services;
using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Runs Dalamud game API calls on the framework thread from HTTP worker threads.
/// </summary>
public sealed class FrameworkThreadDispatcher
{
	private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(2);

	private readonly IFramework framework;

	public FrameworkThreadDispatcher(IFramework framework)
	{
		this.framework = framework;
	}

	public bool TryRun(Action action, out string? error, TimeSpan? timeout = null)
	{
		try
		{
			Task task = this.framework.RunOnFrameworkThread(action);
			if (!task.Wait(timeout ?? DefaultTimeout))
			{
				error = "Framework dispatch timed out.";
				return false;
			}

			error = null;
			return true;
		}
		catch (Exception ex)
		{
			error = ex.Message;
			return false;
		}
	}

	public bool TryRun<T>(Func<T> func, out T? result, out string? error, TimeSpan? timeout = null)
	{
		result = default;
		try
		{
			Task<T> task = this.framework.RunOnFrameworkThread(func);
			if (!task.Wait(timeout ?? DefaultTimeout))
			{
				error = "Framework dispatch timed out.";
				return false;
			}

			result = task.Result;
			error = null;
			return true;
		}
		catch (Exception ex)
		{
			error = ex.Message;
			return false;
		}
	}
}
