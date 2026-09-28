using LLMDesktopAssistant.LLM.Services;

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
				Description = string.Empty,
				IsFixed = true,
				Provider = new SubAgentsSection(services)
			});
		}
	}
}
