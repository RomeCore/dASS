using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.Localization;

namespace LLMDesktopAssistant.Prompting.Context.Providers.SystemSlot
{
	[ChatService(typeof(PromptContextNativeProvider))]
	public class SystemSlotSectionProvider : PromptContextNativeProvider
	{
		public SystemSlotSectionProvider(IServiceProvider services)
		{
			AddContext(new PromptContextInfo
			{
				Name = "system-slot",
				Order = 0,
				Description = "The base system prompt of the agent: the free-form prompt text and the enabled system prompt components.",
				NameKey = Locale.GetKey("prompt.context.name.system-slot"),
				DescriptionKey = Locale.GetKey("prompt.context.description.system-slot"),
				IsFixed = true,
				Provider = new SystemSlotSection(services)
			});
		}
	}
}
