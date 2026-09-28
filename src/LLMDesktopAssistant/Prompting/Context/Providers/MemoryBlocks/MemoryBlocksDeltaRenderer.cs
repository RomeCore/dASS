using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.Prompting.Context.AddonItems;

namespace LLMDesktopAssistant.Prompting.Context.Providers.MemoryBlocks
{
	/// <summary>
	/// Delta renderer of the memory blocks section: projects the transitions into the
	/// 'memory_blocks_system_section_delta' template.
	/// </summary>
	[ChatService(typeof(IPromptSectionDeltaRenderer<MemoryBlocksSectionDelta>))]
	public class MemoryBlocksDeltaRenderer(
		ITemplateLibraryAccessor templates
	) : AddonSectionDeltaRenderer<MemoryBlockItem, MemoryBlockChange, MemoryBlocksSectionDelta>(templates)
	{
		/// <inheritdoc/>
		protected override string TemplateId => "memory_blocks_system_section_delta";

		/// <inheritdoc/>
		protected override object ProjectAddition(AddonItemAddition<MemoryBlockItem, MemoryBlockChange> addition) => new
		{
			name = addition.Name,
			has_definition = addition.Definition is not null,
			without_definition = addition.Definition is null,
			definition_name = addition.Definition?.Name,
			definition_description = addition.Definition?.Description,
			definition_access = addition.Definition is { } definition ? GetAccess(definition.CanRead, definition.CanWrite) : null,
			definition_types = addition.Definition is { } definition2 ? GetTypes(definition2.FactsEnabled, definition2.LogsEnabled) : null,
			description_changed = addition.Changes?.DescriptionChanged ?? false,
			new_description = addition.Changes?.NewDescription,
			access_changed = addition.Changes?.AccessChanged ?? false,
			new_access = addition.Changes is { } changes && changes.AccessChanged ? GetAccess(changes.NewCanRead, changes.NewCanWrite) : null,
			types_changed = addition.Changes?.TypesChanged ?? false,
			new_types = addition.Changes is { } changes2 && changes2.TypesChanged ? GetTypes(changes2.NewFactsEnabled, changes2.NewLogsEnabled) : null
		};

		/// <inheritdoc/>
		protected override object ProjectRemoval(AddonItemRemoval removal) => new
		{
			name = removal.Name
		};

		/// <inheritdoc/>
		protected override object ProjectBecameVisible(AddonItemBecameVisible<MemoryBlockItem, MemoryBlockChange> becameVisible) => new
		{
			name = becameVisible.Name
		};

		/// <inheritdoc/>
		protected override object ProjectUpdate(AddonItemUpdate<MemoryBlockChange> update) => new
		{
			name = update.Name,
			description_changed = update.Changes?.DescriptionChanged ?? false,
			new_description = update.Changes?.NewDescription,
			access_changed = update.Changes?.AccessChanged ?? false,
			new_access = update.Changes is { } changes && changes.AccessChanged ? GetAccess(changes.NewCanRead, changes.NewCanWrite) : null,
			types_changed = update.Changes?.TypesChanged ?? false,
			new_types = update.Changes is { } changes2 && changes2.TypesChanged ? GetTypes(changes2.NewFactsEnabled, changes2.NewLogsEnabled) : null
		};

		private static string GetAccess(bool canRead, bool canWrite) => (canRead, canWrite) switch
		{
			(true, true) => "read-write",
			(true, false) => "read-only",
			(false, true) => "write-only",
			_ => "none"
		};

		private static string GetTypes(bool factsEnabled, bool logsEnabled) => (factsEnabled, logsEnabled) switch
		{
			(true, true) => "facts,logs",
			(true, false) => "facts",
			(false, true) => "logs",
			_ => "none"
		};
	}
}
