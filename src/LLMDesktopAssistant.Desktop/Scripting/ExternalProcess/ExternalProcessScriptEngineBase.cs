using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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
	/// A language engine supplies only what its language dictates — the script text, the document encoding and the
	/// interpreter — while everything a caller depends on (statuses, error text, timeouts, addon invalidation) is
	/// decided here once.
	/// </remarks>
	public abstract class ExternalProcessScriptEngineBase : IExternalProcessScriptEngine
	{
		private readonly IProcessLauncher _processLauncher;

		/// <summary>Initializes a new instance of the <see cref="ExternalProcessScriptEngineBase"/> class.</summary>
		protected ExternalProcessScriptEngineBase(IProcessLauncher processLauncher)
			=> _processLauncher = processLauncher;

		/// <inheritdoc/>
		public abstract ScriptLanguageType Language { get; }

		/// <inheritdoc/>
		public abstract IScriptEngineDescriptor Descriptor { get; }

		/// <summary>The encoding the runtime expects for a script file (a BOM matters for Windows PowerShell 5.1).</summary>
		protected abstract Encoding ScriptEncoding { get; }

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
			var chatSettings = services.GetRequiredService<IChatSettingsService>().Settings;
			var workingDirectory = chatSettings.Environment.GetEffectiveWorkingDirectories().GetWorkingDirectory();

			var inputMode = ResolveInputMode(parameters.InputMode);
			if (inputMode is null)
				return Failure(ExternalProcessStatus.InvalidOutput,
					$"'{Language}' cannot pass a structured value: the addon must declare 'json-input: true'.");

			var script = BuildScript(parameters, inputMode.Value, workingDirectory);
			var scriptPath = CreateScriptFile(script);
			var launched = false;

			try
			{
				var launchParameters = CreateLaunchParameters(chatSettings.Environment, scriptPath, parameters,
					runInTerminal: false).WithTimeout(parameters.TimeOut);

				ProcessDescriptor process;
				try
				{
					process = _processLauncher.Launch(launchParameters, ct);
					launched = true;
				}
				catch (Exception ex)
				{
					Log.Warning(ex, "Failed to launch a {Language} script ({Context}).", Language, parameters.ProcessName);
					return Failure(ExternalProcessStatus.ProcessFailed,
						$"Failed to launch the '{Language}' script: {ex.Message}");
				}

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
						Error = BuildProcessError(stderr, exitCode, launchParameters)
					};
				}

				string? outputError = null;
				var json = parameters.OutputMode is ScriptOutputMode.None
					? null
					: ExtractJson(stdout, parameters.OutputMode, parameters.OutputPattern, out outputError);

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
				TryDelete(scriptPath);

				if (launched)
					InvalidateAddons(parameters.Kind, services);
			}
		}

		/// <inheritdoc/>
		public async Task<ExternalProcessScriptTerminalResult> RunInTerminalAsync(
			ExternalProcessScriptTerminalParameters parameters, IChatAttachmentHost host, IServiceProvider services,
			CancellationToken ct)
		{
			var chatSettings = services.GetRequiredService<IChatSettingsService>().Settings;
			var workingDirectory = chatSettings.Environment.GetEffectiveWorkingDirectories().GetWorkingDirectory();

			var inputMode = ResolveInputMode(parameters.InputMode);
			if (inputMode is null)
			{
				return new ExternalProcessScriptTerminalResult
				{
					Status = ExternalProcessStatus.InvalidOutput,
					Output = string.Empty,
					Error = $"'{Language}' cannot pass a structured value: the addon must declare 'json-input: true'."
				};
			}

			var script = BuildScript(parameters, inputMode.Value, workingDirectory);
			var scriptPath = CreateScriptFile(script);
			var launched = false;

			try
			{
				// An interactive terminal has no deadline and is never linked to the caller's token: one token runs
				// the whole chat, and stopping a generation must not kill a session the user is typing into. The
				// card owns the kill instead (TerminalRun registers it and Leave releases it).
				var launchParameters = CreateLaunchParameters(chatSettings.Environment, scriptPath, parameters,
					runInTerminal: true).WithTimeout(null);

				ProcessDescriptor process;
				try
				{
					process = _processLauncher.Launch(launchParameters, CancellationToken.None);
					launched = true;
				}
				catch (Exception ex)
				{
					Log.Warning(ex, "Failed to launch a {Language} terminal ({Context}).", Language, parameters.ProcessName);
					return new ExternalProcessScriptTerminalResult
					{
						Status = ExternalProcessStatus.ProcessFailed,
						Output = string.Empty,
						Error = $"Failed to launch the '{Language}' script: {ex.Message}"
					};
				}

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
				TryDelete(scriptPath);

				if (launched)
					InvalidateAddons(parameters.Kind, services);
			}
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

		/// <summary>
		/// Takes the promised structure out of standard output. A missing marker and unparsable JSON are both errors:
		/// a promised structure is never degraded into text.
		/// </summary>
		/// <remarks>The capture is non-greedy and spans lines: it ends at the first end marker after the start marker.</remarks>
		private static JsonNode? ExtractJson(string stdout, ScriptOutputMode mode, string? pattern,
			out string? error)
		{
			error = null;

			string payload;
			if (mode is ScriptOutputMode.Whole)
			{
				payload = stdout.Trim();
			}
			else
			{
				if (string.IsNullOrEmpty(pattern))
				{
					error = "The addon declares a pattern output mode but no pattern.";
					return null;
				}

				var markers = SplitPattern(pattern);
				if (markers is null)
				{
					error = $"The output pattern '{pattern}' must contain exactly one unescaped '*'.";
					return null;
				}

				var (begin, end) = markers.Value;
				var start = begin.Length == 0 ? 0 : stdout.IndexOf(begin, StringComparison.Ordinal);
				if (start < 0)
				{
					error = $"The output marker '{begin}' was not found in standard output.";
					return null;
				}

				start += begin.Length;
				var finish = end.Length == 0 ? stdout.Length : stdout.IndexOf(end, start, StringComparison.Ordinal);
				if (finish < 0)
				{
					error = $"The output marker '{end}' was not found after '{begin}'.";
					return null;
				}

				payload = stdout[start..finish].Trim();
			}

			try
			{
				return JsonNode.Parse(payload);
			}
			catch (JsonException ex)
			{
				error = $"The script promised JSON, but its output does not parse: {ex.Message}";
				return null;
			}
		}

		/// <summary>
		/// Splits <c>"&lt;BEGIN&gt;*&lt;END&gt;"</c> at its first unescaped <c>*</c>; <c>\*</c> is a literal asterisk.
		/// </summary>
		/// <returns>The markers, or <see langword="null"/> when the pattern has no unescaped asterisk.</returns>
		private static (string Begin, string End)? SplitPattern(string pattern)
		{
			var begin = new StringBuilder();
			var end = new StringBuilder();
			var afterStar = false;

			for (int i = 0; i < pattern.Length; i++)
			{
				var ch = pattern[i];
				if (ch is '\\' && i + 1 < pattern.Length && pattern[i + 1] is '*')
				{
					(afterStar ? end : begin).Append('*');
					i++;
					continue;
				}

				if (ch is '*' && !afterStar)
				{
					afterStar = true;
					continue;
				}

				(afterStar ? end : begin).Append(ch);
			}

			return afterStar ? (begin.ToString(), end.ToString()) : null;
		}

		private static ExternalProcessScriptResult Failure(ExternalProcessStatus status, string error)
			=> new() { Status = status, Error = error };
	}
}
