using System.IO;
using LLMDesktopAssistant.Desktop.Execution;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.Prompting.Context.Scripting;
using LLMDesktopAssistant.Scripting;
using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.StructuredValues.Const;
using LLMDesktopAssistant.StructuredValues.Converters;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace LLMDesktopAssistant.Desktop.Scripting.Python
{
	/// <summary>
	/// Python implementation of <see cref="IScriptablePromptContextEngine"/>. Handles contexts written in Python
	/// with YAML frontmatter in <c>"""</c> docstring blocks and runs them as a headless external process:
	/// stdout is the context text, the previous rendered text is exposed as the <c>previous</c> global.
	/// Only available on Desktop.
	/// </summary>
	/// <remarks>
	/// The <c>previous</c> preamble is the same channel the Python tool and command engines use for
	/// <c>tool_args</c>/<c>command_args</c>: a child process has no shared state, so the literal is serialized by
	/// <see cref="PythonStructuredSerializer"/> and prepended to the body.
	/// </remarks>
	[Service(typeof(IScriptablePromptContextEngine))]
	public class PythonPromptContextEngine(
		IProcessLauncher processLauncher,
		PythonHelperService pythonHelperService
	) : IScriptablePromptContextEngine
	{
		/// <inheritdoc/>
		public ScriptLanguageType Language => ScriptLanguageType.Python;

		/// <inheritdoc/>
		public IScriptablePromptContextEngineDescriptor Descriptor { get; } = new PythonPromptContextEngineDescriptor();

		/// <inheritdoc/>
		public async Task<string?> RunAsync(IServiceProvider services, string body, string? previous,
			CancellationToken cancellationToken = default)
		{
			var chatSettings = services.GetRequiredService<IChatSettingsService>().Settings;
			var workDir = chatSettings.Environment.GetEffectiveWorkingDirectories().GetWorkingDirectory();

			var previousLiteral = (previous is null ? null : new ConstNodeStringValue { Value = previous })
				.ToPythonString();
			var pythonCode = $"""
				import sys
				sys.stdout.reconfigure(encoding="utf-8")
				sys.stderr.reconfigure(encoding="utf-8")
				previous = {previousLiteral}
				{body}
				""";

			var tempPyFile = Path.GetFullPath(Path.Combine(workDir, $"{Guid.NewGuid()}.py"));
			File.WriteAllText(tempPyFile, pythonCode);

			try
			{
				// Headless: no terminal, no addon invalidation — a context runs on every prompt build.
				var process = processLauncher.Launch(pythonHelperService.CreateLaunchParameters(
					chatSettings.Environment, $"python \"{tempPyFile}\"", "Prompt context", false, true),
					cancellationToken);

				int exitCode = await process;
				if (exitCode == 0)
					return process.Output;

				Log.Warning("Prompt context script exited with code {ExitCode}: {Output}", exitCode, process.Output);
			}
			finally
			{
				File.Delete(tempPyFile);
			}
			return null;
		}
	}

	/// <summary>
	/// The descriptor of the Python scriptable prompt-context engine.
	/// </summary>
	public class PythonPromptContextEngineDescriptor : IScriptablePromptContextEngineDescriptor
	{
		/// <inheritdoc/>
		public ScriptLanguageType Language => ScriptLanguageType.Python;

		/// <inheritdoc/>
		public string[] Extensions => [".py"];

		/// <inheritdoc/>
		public string FrontmatterStart => @"""""""";

		/// <inheritdoc/>
		public string FrontmatterEnd => @"""""""";
	}
}
