using System.Collections.Concurrent;
using System.IO;
using LLMDesktopAssistant.Desktop.Execution;
using LLMDesktopAssistant.LLM.Settings;
using LLMDesktopAssistant.Services;

namespace LLMDesktopAssistant.Desktop.Scripting.Python
{
	/// <summary>
	/// Finds the Python an invocation would actually use, and asks it for its file and version.
	/// </summary>
	public interface IPythonInterpreterLocator
	{
		/// <summary>
		/// Resolves the interpreter behind the chat's environment and reads what it reports about itself.
		/// </summary>
		/// <param name="useAddonEnvironment">
		/// Whether the scriptable addons' venv applies instead of the native tools' one.
		/// </param>
		/// <param name="cancellationToken">Abandons the wait for the probe; the cached probe keeps running.</param>
		Task<PythonInterpreterResolution> ResolveAsync(ChatEnvironmentSettings settings, bool useAddonEnvironment,
			CancellationToken cancellationToken = default);
	}

	/// <summary>
	/// What a chat's python invocation lands on: its file and version, or the reason there is none.
	/// </summary>
	public sealed record PythonInterpreterResolution(string? Version, string? Problem,
		string? ExecutablePath = null)
	{
		/// <summary>
		/// Whether an interpreter answered.
		/// </summary>
		public bool Found => Problem is null;
	}

	/// <summary>
	/// Probes the interpreter through the same launch the scripts use, once per effective venv.
	/// </summary>
	/// <remarks>
	/// Python has no executable to locate - the shell picks one inside the activated venv - so the probe runs
	/// <c>python -</c> with a small program on standard input. The program, not the command line, carries the code,
	/// so the wrapper's quoting cannot break it, and the marker lines say which answer is which: an activation script
	/// that prints something of its own cannot be mistaken for a version. The result is cached per venv path, which
	/// is also what makes a settings change take effect without a restart.
	/// </remarks>
	[Service(typeof(IPythonInterpreterLocator))]
	public sealed class PythonInterpreterLocator(IProcessLauncher processLauncher, PythonHelperService pythonHelperService)
		: IPythonInterpreterLocator
	{
		private const string pathMarker = "@@PATH@@";
		private const string versionMarker = "@@VER@@";

		// Written on one line and without indentation on purpose: a raw string would leave a leading tab, and python
		// refuses an unexpected indent at the top of a module.
		private const string probeCode =
			"import sys\nprint('@@PATH@@' + sys.executable)\nprint('@@VER@@Python ' + sys.version.split()[0])\n";

		private static readonly TimeSpan _probeTimeout = TimeSpan.FromSeconds(30);

		private readonly ConcurrentDictionary<string, Task<PythonInterpreterResolution>> _cache = [];

		/// <inheritdoc/>
		public async Task<PythonInterpreterResolution> ResolveAsync(ChatEnvironmentSettings settings,
			bool useAddonEnvironment, CancellationToken cancellationToken = default)
		{
			var venvPath = EffectiveVenvPath(settings, useAddonEnvironment);

			// The probe never takes the caller's token: one chat's Stop must not poison the cached answer.
			var probe = _cache.GetOrAdd($"{useAddonEnvironment}|{venvPath}",
				_ => ProbeAsync(settings, venvPath, useAddonEnvironment));
			return await probe.WaitAsync(cancellationToken).ConfigureAwait(false);
		}

		private static string EffectiveVenvPath(ChatEnvironmentSettings settings, bool useAddonEnvironment)
		{
			var configuration = settings.EnsureAdditional<PythonEnvironmentConfiguration>();
			var path = useAddonEnvironment
				? configuration.AddonVenvActivateScriptPath ?? configuration.VenvActivateScriptPath
				: configuration.VenvActivateScriptPath;

			return path ?? string.Empty;
		}

		private async Task<PythonInterpreterResolution> ProbeAsync(ChatEnvironmentSettings settings, string venvPath,
			bool useAddonEnvironment)
		{
			// A configured activation script that is not there is not a python problem: name the file instead of
			// letting cmd answer "The system cannot find the path specified".
			if (venvPath.Length > 0 && !File.Exists(venvPath))
			{
				return new PythonInterpreterResolution(null,
					$"The configured venv activation script '{venvPath}' does not exist.");
			}

			try
			{
				var parameters = pythonHelperService
					.CreateLaunchParameters(settings, "python -", "Python Check", runInTerminal: false,
						useAddonEnvironment)
					.WithTimeout(_probeTimeout)
					.WithStdIn(probeCode);
				var process = processLauncher.Launch(parameters, CancellationToken.None);
				var exitCode = await process.ExitCodeTask.ConfigureAwait(false);

				var output = process.PlainOutput;
				IEnumerable<string> stdout = output?.StdOut ?? [];
				IEnumerable<string> stderr = output?.StdErr ?? [];
				var lines = stdout.Concat(stderr).Select(line => line.Trim()).ToList();

				var version = Marked(lines, versionMarker);
				if (exitCode == 0 && version is not null)
					return new PythonInterpreterResolution(version, null, Marked(lines, pathMarker));

				var message = string.Join(Environment.NewLine, lines).Trim();
				return new PythonInterpreterResolution(null, message.Length > 0
					? message
					: $"The python probe exited with code {exitCode} and printed nothing.");
			}
			catch (Exception ex)
			{
				return new PythonInterpreterResolution(null, ex.Message);
			}
		}

		/// <summary>
		/// Reads the value a marker line carries, or <see langword="null"/> when no line carries it.
		/// </summary>
		private static string? Marked(IEnumerable<string> lines, string marker)
		{
			foreach (var line in lines)
			{
				if (line.StartsWith(marker, StringComparison.Ordinal) && line.Length > marker.Length)
					return line[marker.Length..];
			}

			return null;
		}
	}
}
