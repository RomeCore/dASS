using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.Prompting.Context.AddonItems;

namespace LLMDesktopAssistant.Prompting.Context.Providers.MemoryBlocks
{
	/// <summary>
	/// Delta provider of the memory blocks section: the common addon delta engine plus the
	/// block field-level comparison (description, access and types).
	/// </summary>
	[ChatService(typeof(IPromptSectionDeltaProvider<MemoryBlocksSectionState, MemoryBlocksSectionDelta>))]
	public class MemoryBlocksDeltaProvider(
		IPromptSectionStateProvider<MemoryBlocksSectionState> stateProvider
	) : AddonSectionDeltaProvider<MemoryBlocksSectionState, MemoryBlockItem, MemoryBlockChange, MemoryBlocksSectionDelta>(stateProvider)
	{
		/// <inheritdoc/>
		protected override MemoryBlockChange? Diff(MemoryBlockItem known, MemoryBlockItem current)
		{
			bool descriptionChanged = known.Description != current.Description;
			bool accessChanged = known.CanRead != current.CanRead || known.CanWrite != current.CanWrite;
			bool typesChanged = known.FactsEnabled != current.FactsEnabled || known.LogsEnabled != current.LogsEnabled;
			if (!descriptionChanged && !accessChanged && !typesChanged)
				return null;

			return new MemoryBlockChange
			{
				DescriptionChanged = descriptionChanged,
				NewDescription = descriptionChanged ? current.Description : null,
				AccessChanged = accessChanged,
				NewCanRead = accessChanged && current.CanRead,
				NewCanWrite = accessChanged && current.CanWrite,
				TypesChanged = typesChanged,
				NewFactsEnabled = typesChanged && current.FactsEnabled,
				NewLogsEnabled = typesChanged && current.LogsEnabled
			};
		}
	}
}
