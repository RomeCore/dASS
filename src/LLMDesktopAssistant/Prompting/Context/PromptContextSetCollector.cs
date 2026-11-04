using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Agents;
using LLMDesktopAssistant.LLM.Services;

namespace LLMDesktopAssistant.Prompting.Context
{
	[ChatService(typeof(IAddonSetCollector<PromptContextInfo>))]
	public class PromptContextSetCollector(
		IChatSettingsService chatSettings,
		IServiceProvider services
	) : AddonSetCollectorBase<PromptContextInfo, PromptContextChange>(services)
	{
		protected override AddonTarget ResolveAgentTarget(ChatAgentDescriptor agent)
			=> new(agent.Context.GetEffectiveContextSet(chatSettings.Settings), null);
	}
}
