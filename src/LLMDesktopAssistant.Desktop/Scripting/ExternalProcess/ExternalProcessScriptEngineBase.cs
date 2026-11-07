using System.IO;
using System.Text;
using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Addons.Management;
using LLMDesktopAssistant.Desktop.Execution;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.LLM.Settings;
using LLMDesktopAssistant.Scripting;
using LLMDesktopAssistant.Utils;
using LLMDesktopAssistant.Utils.Attachments;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace LLMDesktopAssistant.Desktop.Scripting.ExternalProcess
{
	/// <summary>
	/// The shared lifetime of an out-of-process script: write the generated script to the reserved temp folder, launch
	/// the interpreter, wait, split the channels, extract the optional structure and classify the outcome.
	/// </summary>
	/// <remarks>
	/// A language engine supplies only what its language dictates - the script text, the document encoding and the
	/// interpreter - while everything a caller depends on (statuses, error text, timeouts, addon invalidation) is
	/// decided here once. Starting is split from waiting so a terminal can be handed out as a live handle.
	/// </remarks>
	public abstract class ExternalProcessScriptEngineBase : IExternalProcessScriptEngine
	{
		private static readonly HashSet<string> _noReservedVariableNames = new(StringComparer.Ordinal);

		private readonly IProcessLauncher _processLauncher;

		/// <summary>Initializes a new instance of the <see cref="ExternalProcessScriptEngineBase"/> class.</summary>
		protected ExternalProcessScriptEngineBase(IProcessLauncher processLauncher)
			=> _processLauncher = processLauncher;

		/// <inheritdoc/>
		public abstract IScriptEngineDescriptor Descriptor { get; }

		/// <summary>The encoding the runtime expects for a script file (a BOM matters for Windows PowerShell 5.1).</summary>
		protected abstract Encoding ScriptEncoding { get; }

		/// <summary>
		/// The names the language's own preamble uses, which a caller may not overwrite.
		/// </summary>
		/// <remarks>
		/// The set's comparer carries the language's case rule: PowerShell and batch names are case-insensitive.
		/// </remarks>
		protected virtual IReadOnlySet<string> ReservedVariableNames => _noReservedVariableNames;

		/// <summary>
		/// Builds the whole script: the engine and runtime part, one assignment per variable, then the body.
		/// </summary>
		/// <param name="parameters">The invocation parameters.</param>
		/// <param name="inputMode">The resolved input mode (never <see cref="ScriptInputMode.Auto"/>).</param>
		/// <param name="workingDirectory">The directory the script must treat as its working directory.</param>
		protected abstract string BuildScript(ExternalProcessScriptTerminalParameters parameters, ScriptInputMode inputMode,
			string workingDirectory);

		/// <summary>Builds the interpreter invocation: file, arguments and environment.</summary>
		/// <param name="environment">The chat's environment settings (working directory, venv, ...).</param>
		/// <param name="scriptPath">The temporary script file.</param>
		/// <param name="parameters">The invocation parameters.</param>
		/// <param name="runInTerminal">Whether the interpreter must be launched in a PTY.</param>
		protected abstract ProcessLaunchParameters CreateLaunchParameters(ChatEnvironmentSettings environment,
			string scriptPath, ExternalProcessScriptTerminalParameters parameters, bool runInTerminal);

		/// <inheritdoc/>
		public async Task<ExternalProcessScriptResult> RunAsync(ExternalProcessScriptParameters parameters,
			IServiceProvider services, CancellationToken ct)
		{
			var start = StartScript(parameters, services, runInTerminal: false, parameters.TimeOut, ct);
			if (!start.Started)
			{
				return new ExternalProcessScriptResult
				{
					Status = start.Status,
					Error = start.Error
				};
			}

			var process = start.Process!;
			try
			{
				int exitCode;
				try
				{
					exitCode = await process;
				}
				catch (OperationCanceledException)
				{
					return ct.IsCancellationRequested
						? Failure(ExternalProcessStatus.Cancelled, "The invocation was cancelled.")
						: Failure(ExternalProcessStatus.TimedOut,
							$"The script timed out after {parameters.TimeOut}.");
				}
				catch (Exception ex)
				{
					return Failure(ExternalProcessStatus.ProcessFailed, ex.Message);
				}

				var stdout = Join(process.PlainOutput?.StdOut);
				var stderr = Join(process.PlainOutput?.StdErr);

				if (exitCode != 0)
				{
					return new ExternalProcessScriptResult
					{
						Status = ExternalProcessStatus.ProcessFailed,
						ExitCode = exitCode,
						Stdout = stdout,
						Stderr = stderr,
						Output = process.Output,
						Error = BuildProcessError(stderr, exitCode, start.LaunchParameters!)
					};
				}

				string? outputError = null;
				var json = parameters.OutputMode is ScriptOutputMode.None
					? null
					: ScriptOutputPattern.Extract(stdout, parameters.OutputMode, parameters.OutputPattern, out outputError);

				if (outputError is not null)
				{
					return new ExternalProcessScriptResult
					{
						Status = ExternalProcessStatus.InvalidOutput,
						ExitCode = exitCode,
						Stdout = stdout,
						Stderr = stderr,
						Output = process.Output,
						Error = outputError
					};
				}

				return new ExternalProcessScriptResult
				{
					Status = ExternalProcessStatus.Succeeded,
					ExitCode = exitCode,
					Stdout = stdout,
					Stderr = stderr,
					Output = process.Output,
					Json = json
				};
			}
			finally
			{
				InvalidateAddons(parameters.Kind, services);
			}
		}

		/// <inheritdoc/>
		public async Task<ExternalProcessScriptTerminalResult> RunInTerminalAsync(
			ExternalProcessScriptTerminalParameters parameters, IChatAttachmentHost host, IServiceProvider services,
			CancellationToken ct)
		{
			// An interactive terminal has no deadline and is never linked to the caller's token: one token runs
			// the whole chat, and stopping a generation must not kill a session the user is typing into. The
			// card owns the kill instead (TerminalRun registers it and Leave releases it).
			var start = StartScript(parameters, services, runInTerminal: true, timeOut: null, CancellationToken.None);
			if (!start.Started)
			{
				return new ExternalProcessScriptTerminalResult
				{
					Status = start.Status,
					Output = string.Empty,
					Error = start.Error
				};
			}

			var process = start.Process!;
			try
			{
				// A script terminal never allows Leave: a detached script has nowhere to deliver its result, so the
				// fire-and-forget case is `wait: false` instead.
				var outcome = await TerminalRun.RunAsync(process, host, attachTerminal: true, parameters.Wait,
					leaveAllowed: false, ct);

				if (outcome.Cancelled)
				{
					return new ExternalProcessScriptTerminalResult
					{
						Status = ExternalProcessStatus.Cancelled,
						Output = process.Output,
						Error = "The invocation was cancelled."
					};
				}

				if (outcome.Left)
				{
					return new ExternalProcessScriptTerminalResult
					{
						Status = ExternalProcessStatus.Succeeded,
						Output = TerminalRun.BuildLeftRunningResult(process)
					};
				}

				// A detached run reports no exit code yet: the card learns it later.
				if (outcome.ExitCode is not { } exitCode)
				{
					return new ExternalProcessScriptTerminalResult
					{
						Status = ExternalProcessStatus.Succeeded,
						Output = process.Output
					};
				}

				return new ExternalProcessScriptTerminalResult
				{
					Status = exitCode == 0 ? ExternalProcessStatus.Succeeded : ExternalProcessStatus.ProcessFailed,
					ExitCode = exitCode,
					Output = process.Output,
					Error = exitCode == 0 ? null : $"The script exited with code {exitCode}."
				};
			}
			finally
			{
				InvalidateAddons(parameters.Kind, services);
			}
		}

		/// <inheritdoc/>
		public ExternalProcessScriptStartResult StartInTerminal(ExternalProcessScriptTerminalParameters parameters,
			IServiceProvider services, CancellationToken launchCancellationToken = default)
			=> StartScript(parameters, services, runInTerminal: true, timeOut: null, launchCancellationToken);

		/// <summary>
		/// Builds the document and launches the interpreter, scheduling the document's deletion for the process's exit.
		/// </summary>
		private ExternalProcessScriptStartResult StartScript(ExternalProcessScriptTerminalParameters parameters,
			IServiceProvider services, bool runInTerminal, TimeSpan? timeOut,
			CancellationToken launchCancellationToken)
		{
			var chatSettings = services.GetRequiredService<IChatSettingsService>().Settings;
			var workingDirectory = chatSettings.Environment.GetEffectiveWorkingDirectories().GetWorkingDirectory();

			var inputMode = ResolveInputMode(parameters.InputMode);
			if (inputMode is null)
			{
				return new ExternalProcessScriptStartResult(null, null, ExternalProcessStatus.InvalidOutput,
					$"'{Descriptor.Language}' cannot pass a structured value: the addon must declare 'json-input: true'.");
			}

			// A variable name is part of the generated source, so it is checked before a byte is written.
			foreach (var name in parameters.Variables.Keys)
				ScriptVariableNameValidator.Validate(name, Descriptor.Language.ToString(), ReservedVariableNames);

			string path;
			try
			{
				path = CreateScriptFile(BuildScript(parameters, inputMode.Value, workingDirectory));
			}
			catch (Exception ex)
			{
				Log.Warning(ex, "Failed to build a {Language} script ({Context}).", Descriptor.Language, parameters.ProcessName);
				return new ExternalProcessScriptStartResult(null, null, ExternalProcessStatus.ProcessFailed, ex.Message);
			}

			// An engine that cannot find its interpreter throws while building these parameters, so the reason
			// becomes a result here instead of escaping through the caller.
			ProcessLaunchParameters launchParameters;
			ProcessDescriptor process;
			try
			{
				launchParameters = CreateLaunchParameters(chatSettings.Environment, path, parameters, runInTerminal)
					.WithTimeout(timeOut);
				process = _processLauncher.Launch(launchParameters, launchCancellationToken);
			}
			catch (Exception ex)
			{
				// Nothing was launched, so nothing will ever read the document.
				TryDelete(path);

				Log.Warning(ex, "Failed to launch a {Language} script ({Context}).", Descriptor.Language, parameters.ProcessName);
				return new ExternalProcessScriptStartResult(null, null, ExternalProcessStatus.ProcessFailed,
					$"Failed to launch the '{Descriptor.Language}' script: {ex.Message}");
			}

			// Deletion on exit is the one rule both entry points can honour: a terminal outlives this call.
			_ = DeleteWhenExitedAsync(process, path);

			return new ExternalProcessScriptStartResult(process, launchParameters, ExternalProcessStatus.Succeeded, null);
		}

		/// <summary>
		/// Resolves <see cref="ScriptInputMode.Auto"/> against the engine's capabilities and rejects a structure the
		/// language cannot represent. <see langword="null"/> means "this engine cannot honour the request".
		/// </summary>
		private ScriptInputMode? ResolveInputMode(ScriptInputMode mode) => mode switch
		{
			ScriptInputMode.Auto => Descriptor.Capabilities.HasFlag(ScriptEngineCapabilities.StructuredInput)
				? ScriptInputMode.Structured
				: ScriptInputMode.Json,
			ScriptInputMode.Structured when !Descriptor.Capabilities.HasFlag(ScriptEngineCapabilities.StructuredInput) => null,
			var other => other
		};

		private string CreateScriptFile(string script)
		{
			Directory.CreateDirectory(Directories.TempScripts);

			var extension = Descriptor.Extensions.FirstOrDefault() ?? ".txt";
			var path = Path.Combine(Directories.TempScripts, $"{Guid.NewGuid():N}{extension}");
			File.WriteAllText(path, script, ScriptEncoding);
			return path;
		}

		/// <summary>
		/// Deletes the document once the process is gone, whatever ended it.
		/// </summary>
		private static async Task DeleteWhenExitedAsync(ProcessDescriptor process, string path)
		{
			try
			{
				await process.ExitCodeTask.ConfigureAwait(false);
			}
			catch (Exception)
			{
				// A cancelled or killed process still owns its document until it is gone.
			}

			TryDelete(path);
		}

		private static void TryDelete(string path)
		{
			try
			{
				File.Delete(path);
			}
			catch (Exception ex)
			{
				Log.Warning(ex, "Failed to delete the temporary script '{Path}'.", path);
			}
		}

		private static string Join(IEnumerable<string>? lines) => string.Join(Environment.NewLine, lines ?? []);

		/// <summary>
		/// An external process may have touched addon files. A context runs on every prompt build, so it never
		/// invalidates; a tool and a command do it once, after the process is gone.
		/// </summary>
		private static void InvalidateAddons(ScriptableAddonKind kind, IServiceProvider services)
		{
			if (kind is not (ScriptableAddonKind.Tool or ScriptableAddonKind.SlashCommand))
				return;

			try
			{
				services.GetRequiredService<IAddonManagerInvalidator>().Invalidate(AddonKind.All);
			}
			catch (Exception ex)
			{
				Log.Warning(ex, "Failed to invalidate addons after an external script.");
			}
		}

		private static string BuildProcessError(string stderr, int exitCode, ProcessLaunchParameters launchParameters)
		{
			var message = stderr.Trim();
			if (message.Length > 0)
				return message;

			var commandLine = $"{launchParameters.FileName} {string.Join(' ', launchParameters.Arguments)}";
			if (commandLine.Length > 200)
				commandLine = commandLine[..200] + "…";

			return $"The script exited with code {exitCode} and printed nothing to standard error. Command: {commandLine}";
		}

		private static ExternalProcessScriptResult Failure(ExternalProcessStatus status, string error)
			=> new() { Status = status, Error = error };
	}
}
