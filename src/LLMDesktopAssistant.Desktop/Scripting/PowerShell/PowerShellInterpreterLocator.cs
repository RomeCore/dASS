using System.Collections.Concurrent;
using System.IO;
using LLMDesktopAssistant.Desktop.Execution;
using LLMDesktopAssistant.Services;

namespace LLMDesktopAssistant.Desktop.Scripting.PowerShell
{
	/// <summary>
	/// Finds the PowerShell this machine can actually run a script with.
	/// </summary>
	public interface IPowerShellInterpreterLocator
	{
		/// <summary>
		/// Resolves the interpreter, letting the configured path win over discovery.
		/// </summary>
		/// <param name="configuredPath">The chat's PowerShell path, or <see langword="null"/>/empty to discover one.</param>
		/// <returns>The interpreter or the reason there is none.</returns>
		ExecutableResolution Resolve(string? configuredPath);
	}

	/// <summary>
	/// The documented discovery order: the configured path, <c>pwsh</c>, then Windows PowerShell 5.1.
	/// </summary>
	/// <remarks>
	/// PowerShell 7 is preferred because it is the supported line and because a script written for it does not lose
	/// anything on it, while 5.1 is the one Windows ships without an install. Unlike bash there is no trap to dodge:
	/// both executables are ordinary programs, and on Linux only <c>pwsh</c> exists at all. Results are cached per
	/// configured path.
	/// </remarks>
	[Service(typeof(IPowerShellInterpreterLocator))]
	public sealed class PowerShellInterpreterLocator : IPowerShellInterpreterLocator
	{
		private const string pwshName = "pwsh";
		private const string pwshExecutableName = "pwsh.exe";
		private const string windowsPowerShellExecutableName = "powershell.exe";

		private readonly ConcurrentDictionary<string, ExecutableResolution> _cache = [];

		/// <inheritdoc/>
		public ExecutableResolution Resolve(string? configuredPath)
			=> _cache.GetOrAdd(configuredPath?.Trim() ?? string.Empty, Discover);

		private static ExecutableResolution Discover(string configuredPath)
		{
			if (configuredPath.Length > 0)
			{
				return File.Exists(configuredPath)
					? new ExecutableResolution(configuredPath, null)
					: new ExecutableResolution(null,
						$"The configured PowerShell executable '{configuredPath}' does not exist.");
			}

			if (ExecutableLookup.FindOnPath(OperatingSystem.IsWindows() ? pwshExecutableName : pwshName) is { } pwsh)
				return new ExecutableResolution(pwsh, null);

			if (OperatingSystem.IsWindows())
			{
				// 5.1 lives in a versioned system folder that is normally on PATH, but an operator is free to trim PATH.
				var legacy = ExecutableLookup.FindOnPath(windowsPowerShellExecutableName) ?? SystemWindowsPowerShell();
				if (legacy is not null)
					return new ExecutableResolution(legacy, null);
			}

			return new ExecutableResolution(null,
				"No PowerShell interpreter was found on this machine. Install PowerShell 7 (pwsh) or set the " +
				"PowerShell executable in the chat environment.");
		}

		/// <summary>
		/// The system installation of Windows PowerShell 5.1, for a machine whose <c>PATH</c> no longer names it.
		/// </summary>
		private static string? SystemWindowsPowerShell()
		{
			var system = Environment.GetFolderPath(Environment.SpecialFolder.System);
			if (system.Length == 0)
				return null;

			var candidate = Path.Combine(system, "WindowsPowerShell", "v1.0", windowsPowerShellExecutableName);
			return File.Exists(candidate) ? candidate : null;
		}
	}
}
