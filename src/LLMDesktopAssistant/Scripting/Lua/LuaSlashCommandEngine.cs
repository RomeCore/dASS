using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.SlashCommands;
using LLMDesktopAssistant.SlashCommands.Arguments;
using LLMDesktopAssistant.SlashCommands.Execution;
using LLMDesktopAssistant.SlashCommands.Scripting;
using LLMDesktopAssistant.StructuredValues.Converters;
using Microsoft.Extensions.DependencyInjection;

namespace LLMDesktopAssistant.Scripting.Lua
{
	/// <summary>
	/// Lua implementation of <see cref="IScriptableSlashCommandEngine"/>. Handles commands written in Lua with YAML
	/// frontmatter in <c>--[[ ... ]]</c> blocks.
	/// </summary>
	/// <remarks>
	/// The engine itself is stateless and app-scoped; the chat-scoped <see cref="LuaService"/> is resolved per
	/// execution from the command context, exactly like the Lua tool engine does.
	/// </remarks>
	[Service(typeof(IScriptableSlashCommandEngine))]
	public class LuaSlashCommandEngine : IScriptableSlashCommandEngine
	{
		/// <inheritdoc/>
		public IReadOnlyList<IScriptEngineDescriptor> Descriptors { get; } = [new LuaSlashCommandEngineDescriptor()];

		/// <inheritdoc/>
		public ISlashCommandExecutor CreateExecutor(SlashCommandInfo command, SlashCommandArgumentSchema? argumentSchema)
			=> new LuaScriptCommandExecutor(command, argumentSchema);
	}

	/// <summary>The descriptor of the Lua scriptable-command engine.</summary>
	public class LuaSlashCommandEngineDescriptor : IScriptEngineDescriptor
	{
		/// <inheritdoc/>
		public ScriptLanguageType Language => ScriptLanguageType.Lua;

		/// <inheritdoc/>
		public string[] Extensions => [".lua", ".alua"];

		/// <inheritdoc/>
		public string FrontmatterStart => "--[[";

		/// <inheritdoc/>
		public string FrontmatterEnd => "]]";

		/// <inheritdoc/>
		public string? FrontmatterLinePrefix => null;

		/// <inheritdoc/>
		public ScriptEngineCapabilities Capabilities => ScriptEngineCapabilities.StructuredInput;
	}

	/// <summary>
	/// Runs a Lua command body: it exposes the bound arguments as the <c>command_args</c> table, makes the command's
	/// context ambient (so <c>dass.command.*</c> works) and lets the script mutate the message through
	/// <c>dass.chat</c>/<c>dass.command.context.get_message()</c>.
	/// </summary>
	/// <remarks>
	/// The chunk's first return value, when it is a boolean, becomes <see cref="SlashCommandExecutionResult.Generate"/>:
	/// <c>return false</c> suppresses the generation the frontmatter asked for, <c>return true</c> (or no return value at
	/// all) leaves it untouched. That keeps the declarative <c>generate</c> key as the ceiling while letting the body
	/// decide per run - "nothing to do" does not need the model to wake up and say so.
	/// </remarks>
	internal sealed class LuaScriptCommandExecutor(SlashCommandInfo command, SlashCommandArgumentSchema? argumentSchema)
		: ISlashCommandExecutor
	{
		/// <inheritdoc/>
		public SlashCommandArgumentSchema? ArgumentSchema { get; } = argumentSchema;

		/// <inheritdoc/>
		public async Task<SlashCommandExecutionResult> ExecuteAsync(SlashCommandExecutionContext ctx, CancellationToken ct)
		{
			var luaService = ctx.Services.GetRequiredService<LuaService>();
			LuaExecutionContext.Set(command: ctx);

			// The structured command_args are converted once and shared via the (cloned) globals of this run.
			var commandArgs = LuaStructuredConverter.ToLuaValue(CommandArgsNode.Create(ctx.Arguments, ctx.RawArguments));

			LuaTuple scriptResult;
			try
			{
				scriptResult = await luaService.ExecuteAsync(command.Body, _ => { }, globals =>
				{
					globals["command_args"] = commandArgs;
				}, ct);
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch (Exception ex)
			{
				return new SlashCommandExecutionResult(false, Locale.GetConstKey(ex.Message));
			}

			// Only an explicit boolean return lowers the generation; a script that returns nothing keeps the default.
			var generate = scriptResult.Count > 0 && scriptResult[0] is LuaBoolean returned ? returned.ToBoolean() : true;
			return SlashCommandExecutionResult.Ok(generate);
		}
	}
}
