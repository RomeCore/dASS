using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.Localization;

namespace LLMDesktopAssistant.Prompting.Context.Providers.SubAgents
{
	[ChatService(typeof(PromptContextNativeProvider))]
	public class SubAgentsSectionProvider : PromptContextNativeProvider
	{
		public SubAgentsSectionProvider(IServiceProvider services)
		{
			AddContext(new PromptContextInfo
			{
				Name = "sub-agents",
				Order = 30,
				Description = "The sub-agents the agent can call, with their descriptions and the tools they may use.",
				NameKey = Locale.GetKey("prompt.context.name.sub-agents"),
				DescriptionKey = Locale.GetKey("prompt.context.description.sub-agents"),
				IsFixed = true,
				Provider = new SubAgentsSection(services)
			});
		}
	}
}
