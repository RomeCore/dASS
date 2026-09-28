using LLMDesktopAssistant.LLM.Services.Prompting;

namespace LLMDesktopAssistant.Prompting.Context.Providers.MemoryBlocks
{
	/// <summary>
	/// The memory blocks section: provides the memory blocks for the system prompt.
	/// </summary>
	public class MemoryBlocksSection(IServiceProvider services)
		: PromptAnchoredSectionBase<MemoryBlocksSectionState, MemoryBlocksSectionDelta>(services)
	{
		public override string Discriminator => "memory-blocks";
	}
}
