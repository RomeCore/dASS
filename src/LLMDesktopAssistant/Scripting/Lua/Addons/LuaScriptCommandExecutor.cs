using AsyncLua.Values;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.SlashCommands;
using LLMDesktopAssistant.SlashCommands.Arguments;
using LLMDesktopAssistant.SlashCommands.Execution;
using LLMDesktopAssistant.SlashCommands.Scripting;
using LLMDesktopAssistant.StructuredValues.Converters;

namespace LLMDesktopAssistant.Scripting.Lua.Addons
{
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
	public class LuaScriptCommandExecutor(SlashCommandInfo command, SlashCommandArgumentSchema? argumentSchema)
		: ISlashCommandExecutor
	{
		/// <inheritdoc/>
		public SlashCommandArgumentSchema? ArgumentSchema { get; } = argumentSchema;

		/// <inheritdoc/>
		public async Task<SlashCommandExecutionResult> ExecuteAsync(SlashCommandExecutionContext ctx, CancellationToken ct)
		{
			var luaService = ctx.Services.GetRequiredService<LuaService>();

			// The arguments are converted once and shared via the (cloned) globals of this run. json-input: true passes
			// them as JSON text the body decodes itself; auto and false keep the table.
			var commandArgs = CommandArgsNode.Create(ctx.Arguments, ctx.RawArguments);
			var luaCommandArgs = command.InputMode is ScriptInputMode.Json
				? new LuaString(commandArgs.ToJsonNode()?.ToJsonString() ?? "null")
				: commandArgs.ToLuaValue();

			LuaTuple scriptResult;
			try
			{
				scriptResult = await luaService.ExecuteAsync(command.Body, _ => { }, globals =>
				{
					globals["command_args"] = luaCommandArgs;
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
