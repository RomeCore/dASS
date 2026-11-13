using LLMDesktopAssistant.LLM.MVVM.Additional;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Scripting.Lua;
using LLMDesktopAssistant.SlashCommands.Arguments;
using LLMDesktopAssistant.SlashCommands.Execution;
using Material.Icons;

namespace LLMDesktopAssistant.SlashCommands.Providers
{
	/// <summary>
	/// Runs <c>/lua-reset</c>: rebuilds the chat's shared Lua state from the snapshot taken before any script loaded.
	/// </summary>
	/// <remarks>
	/// A script that overwrites an API member - <c>dass.chat.get_messages</c>, say - leaves the whole chat's Lua state
	/// wrong, and nothing Lua-side can undo it, because the state belongs to <see cref="LuaService"/>. The chip is a
	/// UI-only part and the command generates nothing, so the reset never reaches the model.
	/// </remarks>
	public sealed class LuaResetCommandExecutor : ISlashCommandExecutor
	{
		/// <inheritdoc/>
		public SlashCommandArgumentSchema? ArgumentSchema => null;

		/// <inheritdoc/>
		public Task<SlashCommandExecutionResult> ExecuteAsync(SlashCommandExecutionContext ctx, CancellationToken ct)
		{
			ctx.Services.GetRequiredService<LuaService>().RefreshAllScripts();

			ctx.Message.IsDisabledForAgents = true;
			ctx.Message.AdditionalData.Add(new AdditionalMessagePart
			{
				ChipTitle = Locale.GetKey("command.lua_reset.done"),
				ChipIcon = MaterialIconKind.Restart,
				IsRestorable = false
			});

			return Task.FromResult(SlashCommandExecutionResult.Ok(
				generate: false, modelFacingMode: ModelFacingMode.Neutral));
		}
	}
}
