using LLMDesktopAssistant.LLM.Services;

namespace LLMDesktopAssistant.Prompting.Context.Providers.MemoryBlocks
{
	/// <summary>
	/// Renders the memory blocks section state into a snapshot (text only).
	/// </summary>
	[ChatService(typeof(IPromptSectionStateRenderer<MemoryBlocksSectionState>))]
	public class MemoryBlocksStateRenderer(
		ITemplateLibraryAccessor templates
	) : IPromptSectionStateRenderer<MemoryBlocksSectionState>
	{
		/// <inheritdoc/>
		public SystemPromptSnapshot Render(MemoryBlocksSectionState state)
		{
			return templates.GetTextTemplate("memory_blocks_system_section").Render(new
			{
				memory_blocks = state.Items.Count > 0
					? state.Items.Select(b => new
					{
						name = b.Name,
						description = b.Description,
						can_read = b.CanRead,
						can_write = b.CanWrite,
						facts_enabled = b.FactsEnabled,
						logs_enabled = b.LogsEnabled
					}).ToArray()
					: null,
				hidden_memory_blocks = state.HiddenNames.Count > 0 ? state.HiddenNames.ToArray() : null
			});
		}
	}
}
