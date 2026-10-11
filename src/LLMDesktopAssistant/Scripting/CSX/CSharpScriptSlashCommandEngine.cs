using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.SlashCommands;
using LLMDesktopAssistant.SlashCommands.Arguments;
using LLMDesktopAssistant.SlashCommands.Execution;
using LLMDesktopAssistant.SlashCommands.Scripting;
using LLMDesktopAssistant.StructuredValues.Converters;
using LLMDesktopAssistant.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace LLMDesktopAssistant.Scripting.CSX
{
	/// <summary>
	/// C# Script implementation of <see cref="IScriptableSlashCommandEngine"/>. Handles commands written in C#
	/// (<c>.csx</c>) with YAML frontmatter in <c>/* ... */</c> comment blocks.
	/// </summary>
	[Service(typeof(IScriptableSlashCommandEngine))]
	public class CSharpScriptSlashCommandEngine : IScriptableSlashCommandEngine
	{
		private readonly CSharpScriptService _scriptService;

		/// <summary>
		/// Initializes a new instance of the <see cref="CSharpScriptSlashCommandEngine"/> class.
		/// </summary>
		public CSharpScriptSlashCommandEngine(CSharpScriptService scriptService)
		{
			_scriptService = scriptService;
		}

		/// <inheritdoc/>
		public ScriptLanguageType Language => ScriptLanguageType.CSharpScript;

		/// <inheritdoc/>
		public IScriptableSlashCommandEngineDescriptor Descriptor { get; } = new CSharpScriptSlashCommandEngineDescriptor();

		/// <inheritdoc/>
		public ISlashCommandExecutor CreateExecutor(SlashCommandInfo command, SlashCommandArgumentSchema? argumentSchema)
			=> new CSharpScriptCommandExecutor(command, argumentSchema, _scriptService);
	}

	/// <summary>The descriptor of the C# scriptable-command engine.</summary>
	public class CSharpScriptSlashCommandEngineDescriptor : IScriptableSlashCommandEngineDescriptor
	{
		/// <inheritdoc/>
		public ScriptLanguageType Language => ScriptLanguageType.CSharpScript;

		/// <inheritdoc/>
		public string[] Extensions => [".csx"];

		/// <inheritdoc/>
		public string FrontmatterStart => "/*";

		/// <inheritdoc/>
		public string FrontmatterEnd => "*/";
	}

	/// <summary>
	/// Runs a C# command body in-process with <see cref="CSharpScriptCommandGlobals"/>: the bound arguments as a JSON
	/// tree, the command context and the working directory.
	/// </summary>
	internal sealed class CSharpScriptCommandExecutor(SlashCommandInfo command, SlashCommandArgumentSchema? argumentSchema,
		CSharpScriptService scriptService) : ISlashCommandExecutor
	{
		/// <inheritdoc/>
		public SlashCommandArgumentSchema? ArgumentSchema { get; } = argumentSchema;

		/// <inheritdoc/>
		public async Task<SlashCommandExecutionResult> ExecuteAsync(SlashCommandExecutionContext ctx, CancellationToken ct)
		{
			var workdir = ctx.Services.GetService<IWorkingDirectoryAccessService>()?.GetWorkingDirectory();

			var globals = new CSharpScriptCommandGlobals
			{
				CommandArgs = JsonStructuredConverter.ToJsonNode(CommandArgsNode.Create(ctx.Arguments, ctx.RawArguments)),
				Context = ctx,
				Workdir = workdir ?? Directories.DefaultWorkingDirectory
			};

			try
			{
				await scriptService.RunAsync(command.Body, globals, ct);
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch (Exception ex)
			{
				return new SlashCommandExecutionResult(false, Locale.GetConstKey(ex.Message));
			}

			return SlashCommandExecutionResult.Ok();
		}
	}
}
