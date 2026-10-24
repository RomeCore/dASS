using System.IO;
using LLMDesktopAssistant.Desktop.Execution;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.Prompting.Context;
using LLMDesktopAssistant.StructuredValues.Const;
using LLMDesktopAssistant.StructuredValues.Converters;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace LLMDesktopAssistant.Desktop.Scripting.Python
{
	/// <summary>
	/// The shared runner of a scriptable prompt context written in Python: the body is written to a temporary
	/// module and executed headless, so stdout is the context text and the previous rendered text is exposed as
	/// the <c>previous</c> global.
	/// </summary>
	/// <remarks>
	/// The provider <em>is</em> the run: the base class holds the timeout and the failure isolation (a script must
	/// never break the prompt build), and the concrete provider decides what the text means — a current-request
	/// live tail or a persisted supersede stamp.
	/// </remarks>
	public abstract class PythonScriptContextProviderBase(
		PromptContextInfo addon,
		IProcessLauncher processLauncher,
		PythonHelperService pythonHelperService)
	{
		/// <summary>The context addon this provider runs.</summary>
		protected PromptContextInfo Addon { get; } = addon;

		/// <summary>
		/// Runs the body under the addon's timeout and turns any failure into "no context".
		/// </summary>
		protected async Task<string?> RunAsync(PromptContextExecutionContext context, string? previous,
			CancellationToken cancellationToken)
		{
			using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			timeout.CancelAfter(Addon.TimeoutMs);

			var chatSettings = context.Services.GetRequiredService<IChatSettingsService>().Settings;
			var workDir = chatSettings.Environment.GetEffectiveWorkingDirectories().GetWorkingDirectory();

			var previousLiteral = (previous is null ? null : new ConstNodeStringValue { Value = previous })
				.ToPythonString();
			var pythonCode = $"""
				import sys
				sys.stdout.reconfigure(encoding="utf-8")
				sys.stderr.reconfigure(encoding="utf-8")
				previous = {previousLiteral}
				{Addon.Body}
				""";

			var tempPyFile = Path.GetFullPath(Path.Combine(workDir, $"{Guid.NewGuid()}.py"));

			try
			{
				File.WriteAllText(tempPyFile, pythonCode);

				// Headless: no terminal, no addon invalidation — a context runs on every prompt build.
				var process = processLauncher.Launch(pythonHelperService.CreateLaunchParameters(
					chatSettings.Environment, $"python \"{tempPyFile}\"", "Prompt context", false, true),
					timeout.Token);

				int exitCode = await process;
				if (exitCode == 0)
					return process.Output;

				Log.Warning("Prompt context script exited with code {ExitCode}: {Output}", exitCode, process.Output);
			}
			catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
			{
				Log.Warning("Prompt context script timed out after {Timeout} ms (context '{Context}').",
					Addon.TimeoutMs, Addon.Name);
			}
			catch (Exception ex)
			{
				Log.Warning(ex, "Prompt context script failed (context '{Context}'): {Message}",
					Addon.Name, ex.Message);
			}
			finally
			{
				File.Delete(tempPyFile);
			}

			return null;
		}
	}
}
