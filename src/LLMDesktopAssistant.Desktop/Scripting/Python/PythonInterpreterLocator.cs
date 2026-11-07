using System.Collections.Concurrent;
using LLMDesktopAssistant.Desktop.Execution;
using LLMDesktopAssistant.LLM.Settings;
using LLMDesktopAssistant.Services;

namespace LLMDesktopAssistant.Desktop.Scripting.Python
{
	/// <summary>
	/// Finds the Python an invocation would actually use, and asks it for its version.
	/// </summary>
	public interface IPythonInterpreterLocator
	{
		/// <summary>
		/// Resolves the interpreter behind the chat's environment and reads the version line it prints.
		/// </summary>
		/// <param name="useAddonEnvironment">
		/// Whether the scriptable addons' venv applies instead of the native tools' one.
		/// </param>
		/// <param name="cancellationToken">Abandons the wait for the probe; the cached probe keeps running.</param>
		Task<PythonInterpreterResolution> ResolveAsync(ChatEnvironmentSettings settings, bool useAddonEnvironment,
			CancellationToken cancellationToken = default);
	}

	/// <summary>
	/// What a chat's python invocation lands on: the version line, or the reason there is none.
	/// </summary>
	public sealed record PythonInterpreterResolution(string? Version, string? Problem)
	{
		/// <summary>
		/// Whether an interpreter answered.
		/// </summary>
		public bool Found => Problem is null;
	}

	/// <summary>
	/// Probes <c>python -V</c> through the same launch the scripts use, once per effective venv.
	/// </summary>
	/// <remarks>
	/// Python has no executable to locate - the shell picks one inside the venv - so running the probe is the only way
	/// to learn both that the interpreter exists and what it is. The result is cached per venv path, which is also what
	/// makes a settings change take effect without a restart.
	/// </remarks>
	[Service(typeof(IPythonInterpreterLocator))]
	public sealed class PythonInterpreterLocator(IProcessLauncher processLauncher, PythonHelperService pythonHelperService)
		: IPythonInterpreterLocator
	{
		private static readonly TimeSpan _probeTimeout = TimeSpan.FromSeconds(30);

		private readonly ConcurrentDictionary<string, Task<PythonInterpreterResolution>> _cache = [];

		/// <inheritdoc/>
		public async Task<PythonInterpreterResolution> ResolveAsync(ChatEnvironmentSettings settings,
			bool useAddonEnvironment, CancellationToken cancellationToken = default)
		{
			var key = $"{useAddonEnvironment}|{EffectiveVenvPath(settings, useAddonEnvironment)}";

			// The probe never takes the caller's token: one chat's Stop must not poison the cached answer.
			var probe = _cache.GetOrAdd(key, _ => ProbeAsync(settings, useAddonEnvironment));
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

		private async Task<PythonInterpreterResolution> ProbeAsync(ChatEnvironmentSettings settings,
			bool useAddonEnvironment)
		{
			try
			{
				var launchParameters = pythonHelperService.CreateLaunchParameters(settings, "python -V", "Python Check",
						runInTerminal: false, useAddonEnvironment)
					.WithTimeout(_probeTimeout);
				var process = processLauncher.Launch(launchParameters, CancellationToken.None);
				var exitCode = await process.ExitCodeTask.ConfigureAwait(false);

				var output = process.PlainOutput;
				IReadOnlyList<string> stdout = output?.StdOut ?? [];
				IReadOnlyList<string> stderr = output?.StdErr ?? [];

				// -V prints to stdout since 3.4 and to stderr before it, and the Store stub complains on stderr.
				var version = stdout.Concat(stderr).Select(line => line.Trim()).FirstOrDefault(line => line.Length > 0);

				if (exitCode == 0 && version is not null)
					return new PythonInterpreterResolution(version, null);

				var message = string.Join(Environment.NewLine, stdout.Concat(stderr)).Trim();
				return new PythonInterpreterResolution(null, message.Length > 0
					? message
					: $"python -V exited with code {exitCode} and printed nothing.");
			}
			catch (Exception ex)
			{
				return new PythonInterpreterResolution(null, ex.Message);
			}
		}
	}
}
