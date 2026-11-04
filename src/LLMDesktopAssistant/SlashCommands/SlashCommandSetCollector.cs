using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Addons.Sources;
using LLMDesktopAssistant.LLM.Services;

namespace LLMDesktopAssistant.SlashCommands
{
	/// <summary>
	/// Merges every <see cref="IAdditionalAddonSource{T}"/> of slash commands and applies the chat-level
	/// enablement and override settings. Commands are chat-level and agent-agnostic, so an agent-addressed request
	/// returns the same set instance as the chat-level one.
	/// </summary>
	[ChatService(typeof(IAddonSetCollector<SlashCommandInfo>))]
	public class SlashCommandSetCollector(
		IChatSettingsService chatSettings,
		IServiceProvider services
	) : AddonSetCollectorBase<SlashCommandInfo, SlashCommandChange>(services)
	{
		protected override AddonTarget ResolveChatTarget()
		{
			var settings = chatSettings.Settings.Commands;
			return new(settings.GetEffectiveCommandsSet(), settings);
		}
	}
}
