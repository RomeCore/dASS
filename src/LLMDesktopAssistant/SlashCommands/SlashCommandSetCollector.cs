using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.SlashCommands.Providers;

namespace LLMDesktopAssistant.SlashCommands
{
	/// <summary>
	/// Merges every <see cref="ISlashCommandProvider"/> into the chat's command set and applies the chat-level
	/// enablement settings.
	/// </summary>
	/// <remarks>
	/// Commands are chat-level and agent-agnostic, so <see cref="GetAddonsForChat"/> is the only path the consumers
	/// (autocomplete, resolution) use; <see cref="GetAddonsForAgent"/> is deliberately left unimplemented so that any
	/// attempt to resolve commands per agent is noisy.
	/// </remarks>
	[ChatService(typeof(IAddonSetCollector<SlashCommandInfo>))]
	public class SlashCommandSetCollector(
		IEnumerable<ISlashCommandProvider> providers,
		IChatSettingsService chatSettings,
		IServiceProvider services
	) : AddonSetCollectorBase<SlashCommandInfo, SlashCommandChange>(services)
	{
		protected override IEnumerable<SlashCommandInfo> GetAdditionalAddons()
			=> providers.SelectMany(p => p.GetCommands());

		public override IEnumerable<SlashCommandInfo> GetAddonsForChat()
		{
			var settings = chatSettings.Settings.Commands;
			if (!settings.EnableCommands)
				return [];

			return GetAddonsWithChanges(settings.GetEffectiveCommandsSet(), agent: null);
		}
	}
}
