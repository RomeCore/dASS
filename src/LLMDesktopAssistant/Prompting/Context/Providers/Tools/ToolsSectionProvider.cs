using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.Localization;

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
				Description = "The tools the agent may call, with their names, descriptions and argument schemas.",
				NameKey = Locale.GetKey("prompt.context.name.tools"),
				DescriptionKey = Locale.GetKey("prompt.context.description.tools"),
				IsFixed = true,
				Provider = new ToolsSection(services)
			});
		}
	}
}
