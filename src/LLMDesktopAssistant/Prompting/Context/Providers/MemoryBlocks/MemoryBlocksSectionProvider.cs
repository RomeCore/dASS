using LLMDesktopAssistant.LLM.Services;

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
				Description = string.Empty,
				IsFixed = true,
				Provider = new MemoryBlocksSection(services)
			});
		}
	}
}
