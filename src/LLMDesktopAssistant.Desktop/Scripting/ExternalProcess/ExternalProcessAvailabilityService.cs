using System.Collections.Concurrent;
using LLMDesktopAssistant.Desktop.Execution;
using LLMDesktopAssistant.Desktop.Scripting.Bash;
using LLMDesktopAssistant.Desktop.Scripting.Batch;
using LLMDesktopAssistant.Desktop.Scripting.PowerShell;
using LLMDesktopAssistant.Desktop.Scripting.Python;
using LLMDesktopAssistant.LLM.Settings;
using LLMDesktopAssistant.Scripting;
using LLMDesktopAssistant.Services;

namespace LLMDesktopAssistant.Desktop.Scripting.ExternalProcess
{
	/// <summary>
	/// Answers whether a language can run in this chat and what its interpreter reports about itself.
	/// </summary>
	public interface IExternalProcessAvailabilityService
	{
		/// <summary>
		/// Probes the language's interpreter once per environment and caches the answer.
		/// </summary>
		Task<ScriptEngineAvailability> CheckAsync(ScriptLanguageType language, ChatEnvironmentSettings settings,
			CancellationToken cancellationToken = default);
	}

	/// <summary>
	/// The probes of the out-of-process family: one cached version query per language.
	/// </summary>
	/// <remarks>
	/// Python is answered by <see cref="IPythonInterpreterLocator"/> - the only presence check it has - while the other
	/// three resolve an executable cheaply and then ask it for its version. The cache is keyed by language and
	/// configured path, so a path change takes effect without a restart.
	/// </remarks>
	[Service(typeof(IExternalProcessAvailabilityService))]
	public sealed class ExternalProcessAvailabilityService(
		IPythonInterpreterLocator pythonInterpreterLocator,
		IBashInterpreterLocator bashInterpreterLocator,
		IPowerShellInterpreterLocator powerShellInterpreterLocator,
		IBatchInterpreterLocator batchInterpreterLocator,
		IProcessLauncher processLauncher) : IExternalProcessAvailabilityService
	{
		private static readonly TimeSpan _probeTimeout = TimeSpan.FromSeconds(30);

		private readonly ConcurrentDictionary<string, Task<ScriptEngineAvailability>> _cache = [];

		/// <inheritdoc/>
		public async Task<ScriptEngineAvailability> CheckAsync(ScriptLanguageType language,
			ChatEnvironmentSettings settings, CancellationToken cancellationToken = default)
		{
			var configuredPath = language switch
			{
				ScriptLanguageType.Bash => settings.GetEffectiveAdditionalSettings()
					.OfType<BashEnvironmentConfiguration>().FirstOrDefault()?.ExecutablePath,
				ScriptLanguageType.PowerShell => settings.GetEffectiveAdditionalSettings()
					.OfType<PowerShellEnvironmentConfiguration>().FirstOrDefault()?.ExecutablePath,
				_ => null
			};

			var probe = _cache.GetOrAdd($"{language}|{configuredPath}",
				_ => ProbeAsync(language, settings, configuredPath));
			return await probe.WaitAsync(cancellationToken).ConfigureAwait(false);
		}

		private async Task<ScriptEngineAvailability> ProbeAsync(ScriptLanguageType language,
			ChatEnvironmentSettings settings, string? configuredPath)
		{
			switch (language)
			{
				case ScriptLanguageType.Python:
				{
					// Scripts run in the addons' venv, so that is the environment availability has to describe.
					var resolution = await pythonInterpreterLocator
						.ResolveAsync(settings, useAddonEnvironment: true)
						.ConfigureAwait(false);

					return resolution.Found
						? new ScriptEngineAvailability(true, resolution.Version, null)
						: new ScriptEngineAvailability(false, null, resolution.Problem);
				}
				case ScriptLanguageType.Bash:
				{
					var interpreter = bashInterpreterLocator.Resolve(configuredPath);
					return interpreter.Found
						? await RunProbeAsync(interpreter.Path!, settings, ["--version"]).ConfigureAwait(false)
						: new ScriptEngineAvailability(false, null, interpreter.Problem);
				}
				case ScriptLanguageType.PowerShell:
				{
					var interpreter = powerShellInterpreterLocator.Resolve(configuredPath);
					return interpreter.Found
						? await RunProbeAsync(interpreter.Path!, settings,
							["-NoLogo", "-NoProfile", "-Command", "$PSVersionTable.PSVersion.ToString()"]).ConfigureAwait(false)
						: new ScriptEngineAvailability(false, null, interpreter.Problem);
				}
				case ScriptLanguageType.Batch:
				{
					var interpreter = batchInterpreterLocator.Resolve();
					return interpreter.Found
						? await RunProbeAsync(interpreter.Path!, settings, ["/d", "/c", "ver"]).ConfigureAwait(false)
						: new ScriptEngineAvailability(false, null, interpreter.Problem);
				}
				default:
					return new ScriptEngineAvailability(false, null,
						$"No out-of-process engine is registered for '{language}'.");
			}
		}

		private async Task<ScriptEngineAvailability> RunProbeAsync(string executable,
			ChatEnvironmentSettings settings, string[] arguments)
		{
			try
			{
				var parameters = new ProcessLaunchParameters
				{
					ProcessName = "Interpreter Check",
					FileName = executable,
					Arguments = [.. arguments],
					WorkingDirectory = settings.GetEffectiveWorkingDirectories().GetWorkingDirectory(),
					TimeOut = _probeTimeout
				};
				var process = processLauncher.Launch(parameters, CancellationToken.None);
				var exitCode = await process.ExitCodeTask.ConfigureAwait(false);

				var output = process.PlainOutput;
				IReadOnlyList<string> stdout = output?.StdOut ?? [];
				IReadOnlyList<string> stderr = output?.StdErr ?? [];
				var version = stdout.Concat(stderr).Select(line => line.Trim()).FirstOrDefault(line => line.Length > 0);

				if (exitCode == 0)
					return new ScriptEngineAvailability(true, version, null);

				var message = string.Join(Environment.NewLine, stdout.Concat(stderr)).Trim();
				return new ScriptEngineAvailability(false, null, message.Length > 0
					? message
					: $"'{executable}' exited with code {exitCode} and printed nothing.");
			}
			catch (Exception ex)
			{
				return new ScriptEngineAvailability(false, null, ex.Message);
			}
		}
	}
}
