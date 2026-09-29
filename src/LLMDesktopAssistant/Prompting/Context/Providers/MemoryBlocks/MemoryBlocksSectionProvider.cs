using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.Localization;

namespace LLMDesktopAssistant.Prompting.Context.Providers.MemoryBlocks
{
	[ChatService(typeof(PromptContextNativeProvider))]
	public class MemoryBlocksSectionProvider : PromptContextNativeProvider
	{
		public MemoryBlocksSectionProvider(IServiceProvider services)
		{
			AddContext(new PromptContextInfo
			{
				Name = "memory-blocks",
				Order = 40,
				Description = "The memory blocks the agent can read and write through the memory tools.",
				NameKey = Locale.GetKey("prompt.context.name.memory-blocks"),
				DescriptionKey = Locale.GetKey("prompt.context.description.memory-blocks"),
				IsFixed = true,
				Provider = new MemoryBlocksSection(services)
			});
		}
	}
}
