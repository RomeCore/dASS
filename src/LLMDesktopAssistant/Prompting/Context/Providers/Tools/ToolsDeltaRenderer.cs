using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.Prompting.Context.AddonItems;

namespace LLMDesktopAssistant.Prompting.Context.Providers.Tools
{
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
}
