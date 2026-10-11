using System.IO;
using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Addons.Management;
using LLMDesktopAssistant.Desktop.Execution;
using LLMDesktopAssistant.LLM.MVVM.Additional;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Scripting;
using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.SlashCommands;
using LLMDesktopAssistant.SlashCommands.Arguments;
using LLMDesktopAssistant.SlashCommands.Execution;
using LLMDesktopAssistant.SlashCommands.Scripting;
using LLMDesktopAssistant.StructuredValues.Converters;
using Microsoft.Extensions.DependencyInjection;

namespace LLMDesktopAssistant.Desktop.Scripting.Python
{
	/// <summary>
	/// Python implementation of <see cref="IScriptableSlashCommandEngine"/>. Handles commands written in Python with
	/// YAML frontmatter in <c>"""</c> docstring blocks and runs them as an external process.
	/// </summary>
	/// <remarks>
	/// Deliberately a thin pipe: there is no chat API for Python. The bound arguments are prepended as the
	/// <c>command_args</c> dict and the process' stdout becomes one <see cref="AdditionalMessageContentPart"/>; a
	/// non-zero exit code becomes the message error.
	/// </remarks>
	[Service(typeof(IScriptableSlashCommandEngine))]
	public class PythonSlashCommandEngine : IScriptableSlashCommandEngine
	{
		private readonly IProcessLauncher _processLauncher;
		private readonly PythonHelperService _pythonHelperService;

		/// <summary>Initializes a new instance of the <see cref="PythonSlashCommandEngine"/> class.</summary>
		public PythonSlashCommandEngine(IProcessLauncher processLauncher, PythonHelperService pythonHelperService)
		{
			_processLauncher = processLauncher;
			_pythonHelperService = pythonHelperService;
		}

		/// <inheritdoc/>
		public ScriptLanguageType Language => ScriptLanguageType.Python;

		/// <inheritdoc/>
		public IScriptableSlashCommandEngineDescriptor Descriptor { get; } = new PythonSlashCommandEngineDescriptor();

		/// <inheritdoc/>
		public ISlashCommandExecutor CreateExecutor(SlashCommandInfo command, SlashCommandArgumentSchema? argumentSchema)
			=> new PythonScriptCommandExecutor(command, argumentSchema, _processLauncher, _pythonHelperService);
	}

	/// <summary>The descriptor of the Python scriptable-command engine.</summary>
	public class PythonSlashCommandEngineDescriptor : IScriptableSlashCommandEngineDescriptor
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

	/// <summary>
	/// Runs a Python command body as an external process, prepending the <c>command_args</c> dict and turning stdout
	/// into a content part.
	/// </summary>
	internal sealed class PythonScriptCommandExecutor(SlashCommandInfo command, SlashCommandArgumentSchema? argumentSchema,
		IProcessLauncher processLauncher, PythonHelperService pythonHelperService) : ISlashCommandExecutor
	{
		/// <inheritdoc/>
		public SlashCommandArgumentSchema? ArgumentSchema { get; } = argumentSchema;

		/// <inheritdoc/>
		public async Task<SlashCommandExecutionResult> ExecuteAsync(SlashCommandExecutionContext ctx, CancellationToken ct)
		{
			var chatSettings = ctx.Services.GetRequiredService<IChatSettingsService>().Settings;
			var workDir = chatSettings.Environment.GetEffectiveWorkingDirectories().GetWorkingDirectory();

			var commandArgs = PythonStructuredSerializer.ToPythonString(CommandArgsNode.Create(ctx.Arguments, ctx.RawArguments));
			var pythonCode = $"""
				import sys
				sys.stdout.reconfigure(encoding="utf-8")
				sys.stderr.reconfigure(encoding="utf-8")
				command_args = {commandArgs}
				{command.Body}
				""";

			var tempPyFile = Path.GetFullPath(Path.Combine(workDir, $"{Guid.NewGuid()}.py"));
			File.WriteAllText(tempPyFile, pythonCode);

			var addonInvalidator = ctx.Services.GetRequiredService<IAddonManagerInvalidator>();

			try
			{
				var process = processLauncher.Launch(pythonHelperService.CreateLaunchParameters(
					chatSettings.Environment, $"python \"{tempPyFile}\"", "Python Command", false, true), ct);
				addonInvalidator.Invalidate(AddonKind.All);

				int exitCode = await process;
				ctx.Message.AdditionalData.Add(new AdditionalMessageContentPart
				{
					Content = process.Output,
					ChipTitle = Locale.GetConstKey(command.Name),
					IsRestorable = false
				});

				return exitCode == 0
					? SlashCommandExecutionResult.Ok(generate: true, modelFacingMode: ModelFacingMode.Raw)
					: new SlashCommandExecutionResult(false, Locale.GetConstKey($"Python exited with code {exitCode}."));
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch (Exception ex)
			{
				return new SlashCommandExecutionResult(false, Locale.GetConstKey(ex.Message));
			}
			finally
			{
				File.Delete(tempPyFile);
			}
		}
	}
}
