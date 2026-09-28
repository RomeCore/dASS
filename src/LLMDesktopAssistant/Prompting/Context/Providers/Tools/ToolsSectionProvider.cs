using LLMDesktopAssistant.LLM.Services;

namespace LLMDesktopAssistant.Prompting.Context.Providers.Tools
{
	[ChatService(typeof(PromptContextNativeProvider))]
	public class ToolsSectionProvider : PromptContextNativeProvider
	{
		public ToolsSectionProvider(IServiceProvider services)
		{
			AddContext(new PromptContextInfo
			{
				Name = "tools",
				Order = 100,
				Description = string.Empty,
				IsFixed = true,
				Provider = new ToolsSection(services)
			});
		}
	}
}
