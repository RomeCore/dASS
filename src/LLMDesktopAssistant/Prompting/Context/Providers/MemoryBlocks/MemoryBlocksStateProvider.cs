using LLMDesktopAssistant.Agents;
using LLMDesktopAssistant.LLM.Services;

namespace LLMDesktopAssistant.Prompting.Context.Providers.MemoryBlocks
{
	/// <summary>
	/// Captures the memory blocks section state by collecting the memory blocks enabled for the agent.
	/// </summary>
	[ChatService(typeof(IPromptSectionStateProvider<MemoryBlocksSectionState>))]
	public class MemoryBlocksStateProvider(
		IChatSettingsService chatSettings
		) : IPromptSectionStateProvider<MemoryBlocksSectionState>
	{
		/// <inheritdoc/>
		public MemoryBlocksSectionState CaptureState(ChatAgentDescriptor agent)
		{
			var options = chatSettings.Settings.Memory.GetEffectiveMemoryOptions();
			var items = options.EnableMemory && options.ManualControlEnabled && agent.Memory.EnableMemory
				? agent.Memory.GetEnabledBlocks(chatSettings.Settings)
					.Select(b => new MemoryBlockItem
					{
						Name = b.Block.Name,
						Description = b.Block.Description,
						CanRead = b.Attachment.AllowsReading(),
						CanWrite = b.Attachment.AllowsWriting(),
						FactsEnabled = b.Block.FactsEnabled,
						LogsEnabled = b.Block.LogsEnabled
					})
					.ToList()
				: [];

			return new MemoryBlocksSectionState
			{
				Items = [.. items.OrderBy(b => b.Name, StringComparer.Ordinal)]
			};
		}
	}
}
