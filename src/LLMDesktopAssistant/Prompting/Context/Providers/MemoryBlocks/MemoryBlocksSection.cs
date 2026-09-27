using LLMDesktopAssistant.Agents;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.LLM.Services.Prompting;
using LLMDesktopAssistant.Prompting.Context.AddonItems;

namespace LLMDesktopAssistant.Prompting.Context.Providers.MemoryBlocks
{
	/// <summary>
	/// The serializable snapshot of a memory block tracked by the memory blocks section.
	/// </summary>
	public class MemoryBlockItem : AddonSectionItem
	{
		/// <summary>
		/// Whether the block allows reading.
		/// </summary>
		public bool CanRead { get; set; }

		/// <summary>
		/// Whether the block allows writing.
		/// </summary>
		public bool CanWrite { get; set; }

		/// <summary>
		/// Whether the facts type is enabled for the block.
		/// </summary>
		public bool FactsEnabled { get; set; }

		/// <summary>
		/// Whether the logs type is enabled for the block.
		/// </summary>
		public bool LogsEnabled { get; set; }
	}

	/// <summary>
	/// The field-level description of a memory block change: the common description field lives
	/// in the base, the access pair and the types pair are block-specific.
	/// </summary>
	public class MemoryBlockChange : AddonItemChange<MemoryBlockItem>
	{
		/// <summary>
		/// Whether the block access (read/write pair) has changed.
		/// </summary>
		public bool AccessChanged { get; set; }

		/// <summary>
		/// The new read permission (when <see cref="AccessChanged"/> is true).
		/// </summary>
		public bool NewCanRead { get; set; }

		/// <summary>
		/// The new write permission (when <see cref="AccessChanged"/> is true).
		/// </summary>
		public bool NewCanWrite { get; set; }

		/// <summary>
		/// Whether the block types (facts/logs pair) have changed.
		/// </summary>
		public bool TypesChanged { get; set; }

		/// <summary>
		/// The new facts flag (when <see cref="TypesChanged"/> is true).
		/// </summary>
		public bool NewFactsEnabled { get; set; }

		/// <summary>
		/// The new logs flag (when <see cref="TypesChanged"/> is true).
		/// </summary>
		public bool NewLogsEnabled { get; set; }

		protected override void ApplyExtra(MemoryBlockItem item)
		{
			if (AccessChanged)
			{
				item.CanRead = NewCanRead;
				item.CanWrite = NewCanWrite;
			}
			if (TypesChanged)
			{
				item.FactsEnabled = NewFactsEnabled;
				item.LogsEnabled = NewLogsEnabled;
			}
		}
	}

	/// <summary>
	/// The state of the memory blocks section: the memory blocks attached to the agent.
	/// </summary>
	public class MemoryBlocksSectionState : AddonSectionState<MemoryBlockItem>
	{
	}

	/// <summary>
	/// The delta of the memory blocks section (see <see cref="AddonSectionDelta{TItem, TChange}"/>).
	/// </summary>
	public class MemoryBlocksSectionDelta : AddonSectionDelta<MemoryBlockItem, MemoryBlockChange>
	{
	}

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

	/// <summary>
	/// The memory blocks section: provides the memory blocks for the system prompt.
	/// </summary>
	public class MemoryBlocksSection(IServiceProvider services)
		: PromptAnchoredSectionBase<MemoryBlocksSectionState, MemoryBlocksSectionDelta>(services)
	{
		public override string Discriminator => "memory-blocks";
	}

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
