// © DeadlyDeathAngel.
// Licensed under the MIT license.

namespace AnamnesisBridge.Services;

using System;
using System.Reflection;

/// <summary>
/// Assembly version surfaced on /health so reloads are obvious (four-part step versions).
/// </summary>
public static class BridgeVersion
{
	public static string Current { get; } = Resolve();

	private static string Resolve()
	{
		Assembly assembly = typeof(BridgeVersion).Assembly;
		string? informational = assembly
			.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
			?.InformationalVersion;
		if (!string.IsNullOrWhiteSpace(informational))
		{
			// Strip any +git metadata.
			int plus = informational.IndexOf('+');
			return plus >= 0 ? informational[..plus] : informational;
		}

		Version? version = assembly.GetName().Version;
		return version?.ToString(4) ?? "0.0.0.0";
	}
}
