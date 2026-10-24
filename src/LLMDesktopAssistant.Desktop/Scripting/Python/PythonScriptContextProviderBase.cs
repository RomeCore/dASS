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
	/// The provider <em>is</em> the run: the concrete provider decides what the text means — a current-request
	/// live tail or a persisted supersede stamp. This base only knows how to execute a Python body; nothing here
	/// arms a timeout or catches anything — the budget and the failure isolation belong to the caller
	/// (<c>PromptContextBudget</c>), so every language is treated identically.
	/// </remarks>
	public abstract class PythonScriptContextProviderBase(
		PromptContextInfo promptContext,
		IProcessLauncher processLauncher,
		PythonHelperService pythonHelperService)
	{
		/// <summary>The context addon this provider runs.</summary>
		protected PromptContextInfo PromptContext { get; } = promptContext;

		/// <summary>Runs the body as a headless process and returns what the process printed.</summary>
		protected async Task<string?> RunAsync(PromptContextExecutionContext context, string? previous,
			CancellationToken cancellationToken)
		{
			var chatSettings = context.Services.GetRequiredService<IChatSettingsService>().Settings;
			var workDir = chatSettings.Environment.GetEffectiveWorkingDirectories().GetWorkingDirectory();

			var previousLiteral = (previous is null ? null : new ConstNodeStringValue { Value = previous })
				.ToPythonString();
			var pythonCode = $"""
				import sys
				sys.stdout.reconfigure(encoding="utf-8")
				sys.stderr.reconfigure(encoding="utf-8")
				previous = {previousLiteral}
				{PromptContext.Body}
				""";

			var tempPyFile = Path.GetFullPath(Path.Combine(workDir, $"{Guid.NewGuid()}.py"));

			try
			{
				File.WriteAllText(tempPyFile, pythonCode);

				// Headless: no terminal, no addon invalidation — a context runs on every prompt build.
				var process = processLauncher.Launch(pythonHelperService.CreateLaunchParameters(
					chatSettings.Environment, $"python \"{tempPyFile}\"", "Prompt context", false, true),
					cancellationToken);

				int exitCode = await process;

				// No JSON channel here: the context text is the merged output, exactly as a terminal would show it.
				if (exitCode == 0)
					return process.Output;

				Log.Warning("Prompt context script exited with code {ExitCode} (context '{Context}'): {Output}",
					exitCode, PromptContext.Name, process.Output);
				return null;
			}
			finally
			{
				File.Delete(tempPyFile);
			}
		}
	}
}
