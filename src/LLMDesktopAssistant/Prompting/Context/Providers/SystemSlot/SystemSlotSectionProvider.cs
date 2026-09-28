using LLMDesktopAssistant.LLM.Services;

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
				Description = string.Empty,
				IsFixed = true,
				Provider = new SystemSlotSection(services)
			});
		}
	}
}
