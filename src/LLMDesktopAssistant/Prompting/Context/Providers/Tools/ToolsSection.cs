using LLMDesktopAssistant.Agents;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.LLM.Services.Prompting;
using LLMDesktopAssistant.LLM.Services.Tools;
using LLMDesktopAssistant.Prompting.Context.AddonItems;

namespace LLMDesktopAssistant.Prompting.Context.Providers.Tools
{
	/// <summary>
	/// The serializable snapshot of a tool addon tracked by the tools section.
	/// </summary>
	public class ToolItem : AddonSectionItem
	{
		/// <summary>
		/// The tool argument schema (a JSON string).
		/// </summary>
		public string ArgumentSchema { get; set; } = "{}";
	}

	/// <summary>
	/// The field-level description of a tool definition change: the common description/body fields
	/// live in the base, only the argument schema is tool-specific.
	/// </summary>
	public class ToolChange : AddonItemChange<ToolItem>
	{
		/// <summary>
		/// Whether the tool argument schema has changed.
		/// </summary>
		public bool ArgumentSchemaChanged { get; set; }

		/// <summary>
		/// The new tool argument schema (when <see cref="ArgumentSchemaChanged"/> is true).
		/// </summary>
		public string? NewArgumentSchema { get; set; }

		protected override void ApplyExtra(ToolItem item)
		{
			if (ArgumentSchemaChanged)
				item.ArgumentSchema = NewArgumentSchema ?? item.ArgumentSchema;
		}
	}

	/// <summary>
	/// The state of the tools section: the visible tools and the names of the hidden tools.
	/// </summary>
	public class ToolsSectionState : AddonSectionState<ToolItem>
	{
	}

	/// <summary>
	/// The delta of the tools section: tool availability transitions, visibility gains
	/// and definition changes (see <see cref="AddonSectionDelta{TItem, TChange}"/>).
	/// </summary>
	public class ToolsSectionDelta : AddonSectionDelta<ToolItem, ToolChange>
	{
	}

	/// <summary>
	/// Captures the tools section state: the visible tools of the agent and the names of its hidden tools.
	/// The hidden names are captured in the hybrid mode only.
	/// The cache is invalidated by the prompt composer, not by this provider.
	/// </summary>
	[ChatService(typeof(IPromptSectionStateProvider<ToolsSectionState>))]
	public class ToolsStateProvider(
		IToolsetCacheService toolsetCache) : IPromptSectionStateProvider<ToolsSectionState>
	{
		/// <inheritdoc/>
		public ToolsSectionState CaptureState(ChatAgentDescriptor agent)
		{
			var items = new List<ToolItem>();
			var hiddenNames = new List<string>();

			// The hidden tools are listed in the prompt in the hybrid mode only:
			// outside of it deltas are not computed at all, so the hidden list has no purpose there.
			bool captureHiddenTools = agent.Context.PromptMode == PromptContextMode.Hybrid;

			foreach (var tool in toolsetCache.ValidTools.Values)
			{
				if (tool.Hidden ?? false)
				{
					if (captureHiddenTools)
						hiddenNames.Add(tool.Name);
				}
				else
				{
					items.Add(new ToolItem
					{
						Name = tool.Name,
						Description = tool.Description,
						ArgumentSchema = tool.ArgumentSchema.ToJsonString()
					});
				}
			}

			return new ToolsSectionState
			{
				Items = [.. items.OrderBy(t => t.Name, StringComparer.Ordinal)],
				HiddenNames = [.. hiddenNames.OrderBy(n => n, StringComparer.Ordinal)]
			};
		}
	}

	/// <summary>
	/// Renders the tools section state into a snapshot: the tool definitions go to the tool list,
	/// while the hidden tools are announced by name in the system prompt text
	/// (with a nudge to inspect them via `addon-info`).
	/// </summary>
	[ChatService(typeof(IPromptSectionStateRenderer<ToolsSectionState>))]
	public class ToolsStateRenderer(
		ITemplateLibraryAccessor templates
	) : IPromptSectionStateRenderer<ToolsSectionState>
	{
		/// <inheritdoc/>
		public SystemPromptSnapshot Render(ToolsSectionState state) => new()
		{
			Text = templates.GetTextTemplate("tools_system_section").Render(new
			{
				hidden_tools = state.HiddenNames.Count > 0 ? state.HiddenNames.ToArray() : null
			}),
			Tools = [.. state.Items.Select(ToSerializableToolDefinition)]
		};

		private static SerializableToolDefinition ToSerializableToolDefinition(ToolItem item) => new()
		{
			Name = item.Name,
			Description = item.Description ?? string.Empty,
			ArgumentSchema = item.ArgumentSchema
		};
	}

	/// <summary>
	/// Delta provider of the tools section: the common addon delta engine plus the tool
	/// field-level comparison (description and argument schema).
	/// </summary>
	[ChatService(typeof(IPromptSectionDeltaProvider<ToolsSectionState, ToolsSectionDelta>))]
	public class ToolsDeltaProvider(
		IPromptSectionStateProvider<ToolsSectionState> stateProvider
	) : AddonSectionDeltaProvider<ToolsSectionState, ToolItem, ToolChange, ToolsSectionDelta>(stateProvider)
	{
		/// <inheritdoc/>
		protected override ToolChange? Diff(ToolItem known, ToolItem current)
		{
			bool descriptionChanged = known.Description != current.Description;
			bool argumentSchemaChanged = known.ArgumentSchema != current.ArgumentSchema;
			if (!descriptionChanged && !argumentSchemaChanged)
				return null;

			return new ToolChange
			{
				DescriptionChanged = descriptionChanged,
				NewDescription = descriptionChanged ? current.Description : null,
				ArgumentSchemaChanged = argumentSchemaChanged,
				NewArgumentSchema = argumentSchemaChanged ? current.ArgumentSchema : null
			};
		}
	}

	/// <summary>
	/// Delta renderer of the tools section: projects the transitions into the
	/// 'tools_system_section_delta' template.
	/// </summary>
	[ChatService(typeof(IPromptSectionDeltaRenderer<ToolsSectionDelta>))]
	public class ToolsDeltaRenderer(
		ITemplateLibraryAccessor templates
	) : AddonSectionDeltaRenderer<ToolItem, ToolChange, ToolsSectionDelta>(templates)
	{
		/// <inheritdoc/>
		protected override string TemplateId => "tools_system_section_delta";

		/// <inheritdoc/>
		protected override object ProjectAddition(AddonItemAddition<ToolItem, ToolChange> addition) => new
		{
			name = addition.Name,
			hidden = addition.Hidden,
			has_definition = addition.Definition is not null,
			without_definition = !addition.Hidden && addition.Definition is null,
			definition_name = addition.Definition?.Name,
			definition_description = addition.Definition?.Description,
			definition_arguments = addition.Definition?.ArgumentSchema,
			description_changed = addition.Changes?.DescriptionChanged ?? false,
			new_description = addition.Changes?.NewDescription,
			arguments_changed = addition.Changes?.ArgumentSchemaChanged ?? false,
			new_arguments = addition.Changes?.NewArgumentSchema
		};

		/// <inheritdoc/>
		protected override object ProjectRemoval(AddonItemRemoval removal) => new
		{
			name = removal.Name
		};

		/// <inheritdoc/>
		protected override object ProjectBecameVisible(AddonItemBecameVisible<ToolItem, ToolChange> becameVisible) => new
		{
			name = becameVisible.Name,
			has_definition = becameVisible.Definition is not null,
			without_definition = becameVisible.Definition is null,
			definition_name = becameVisible.Definition?.Name,
			definition_description = becameVisible.Definition?.Description,
			definition_arguments = becameVisible.Definition?.ArgumentSchema,
			description_changed = becameVisible.Changes?.DescriptionChanged ?? false,
			new_description = becameVisible.Changes?.NewDescription,
			arguments_changed = becameVisible.Changes?.ArgumentSchemaChanged ?? false,
			new_arguments = becameVisible.Changes?.NewArgumentSchema
		};

		/// <inheritdoc/>
		protected override object ProjectUpdate(AddonItemUpdate<ToolChange> update) => new
		{
			name = update.Name,
			description_changed = update.Changes?.DescriptionChanged ?? false,
			new_description = update.Changes?.NewDescription,
			arguments_changed = update.Changes?.ArgumentSchemaChanged ?? false,
			new_arguments = update.Changes?.NewArgumentSchema
		};
	}

	/// <summary>
	/// The tools section: provides the tool definitions for the system prompt.
	/// </summary>
	public class ToolsSection(IServiceProvider services)
		: PromptAnchoredSectionBase<ToolsSectionState, ToolsSectionDelta>(services)
	{
		public override string Discriminator => "tools";
	}

	[ChatService(typeof(PromptContextNativeProvider))]
	public class ToolsSectionProvider : PromptContextNativeProvider
	{
		public ToolsSectionProvider(IServiceProvider services)
		{
			AddContext(new PromptContextInfo
			{
				Name = "tools",
				Order = 100,
				Description = string.Empty,
				IsFixed = true,
				Provider = new ToolsSection(services)
			});
		}
	}
}
