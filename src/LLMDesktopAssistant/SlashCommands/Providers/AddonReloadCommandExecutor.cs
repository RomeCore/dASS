using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Addons.Management;
using LLMDesktopAssistant.Controls.Icons;
using LLMDesktopAssistant.LLM.MVVM.Additional;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.SlashCommands.Arguments;
using LLMDesktopAssistant.SlashCommands.Execution;
using Material.Icons;

namespace LLMDesktopAssistant.SlashCommands.Providers
{
	/// <summary>
	/// Runs <c>/addon-reload</c>: forces every addon manager (app and chat) to reload and the pack locators to rescan,
	/// then marks the produced message with a lightbulb chip.
	/// </summary>
	/// <remarks>
	/// The chat-scoped <see cref="IAddonManagerInvalidator"/> reloads both the app and the chat managers, so a single
	/// call reaches the global packs too. The chip is a UI-only <see cref="AdditionalMessagePart"/> (not a content
	/// part), and the command produces no generation, so nothing about the reload reaches the model.
	/// </remarks>
	public sealed class AddonReloadCommandExecutor : ISlashCommandExecutor
	{
		/// <inheritdoc/>
		public SlashCommandArgumentSchema? ArgumentSchema => null;

		/// <inheritdoc/>
		public Task<SlashCommandExecutionResult> ExecuteAsync(SlashCommandExecutionContext ctx, CancellationToken ct)
		{
			var invalidator = ctx.Services.GetRequiredService<IAddonManagerInvalidator>();
			invalidator.Reload(AddonKind.All);

			ctx.Message.IsDisabledForAgents = true;
			ctx.Message.AdditionalData.Add(new AdditionalMessagePart
			{
				ChipTitle = Locale.GetKey("command.addon_reload.done"),
				ChipIcon = (VisualIconKind)MaterialIconKind.LightbulbOutline,
				IsRestorable = false
			});

			return Task.FromResult(SlashCommandExecutionResult.Ok(
				generate: false, modelFacingMode: ModelFacingMode.Neutral));
		}
	}
}
