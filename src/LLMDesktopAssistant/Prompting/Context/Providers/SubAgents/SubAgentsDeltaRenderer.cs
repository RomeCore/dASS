using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.Prompting.Context.AddonItems;

namespace LLMDesktopAssistant.Prompting.Context.Providers.SubAgents
{
	/// <summary>
	/// Delta renderer of the sub-agents section: projects the transitions into the
	/// 'sub_agents_system_section_delta' template.
	/// </summary>
	[ChatService(typeof(IPromptSectionDeltaRenderer<SubAgentsSectionDelta>))]
	public class SubAgentsDeltaRenderer(
		ITemplateLibraryAccessor templates
	) : AddonSectionDeltaRenderer<SubAgentItem, SubAgentChange, SubAgentsSectionDelta>(templates)
	{
		/// <inheritdoc/>
		protected override string TemplateId => "sub_agents_system_section_delta";

		/// <inheritdoc/>
		protected override object ProjectAddition(AddonItemAddition<SubAgentItem, SubAgentChange> addition) => new
		{
			name = addition.Name,
			hidden = addition.Hidden,
			has_definition = addition.Definition is not null,
			without_definition = !addition.Hidden && addition.Definition is null,
			definition_name = addition.Definition?.Name,
			definition_description = addition.Definition?.Description,
			description_changed = addition.Changes?.DescriptionChanged ?? false,
			new_description = addition.Changes?.NewDescription
		};

		/// <inheritdoc/>
		protected override object ProjectRemoval(AddonItemRemoval removal) => new
		{
			name = removal.Name
		};

		/// <inheritdoc/>
		protected override object ProjectBecameVisible(AddonItemBecameVisible<SubAgentItem, SubAgentChange> becameVisible) => new
		{
			name = becameVisible.Name,
			has_definition = becameVisible.Definition is not null,
			without_definition = becameVisible.Definition is null,
			definition_name = becameVisible.Definition?.Name,
			definition_description = becameVisible.Definition?.Description,
			description_changed = becameVisible.Changes?.DescriptionChanged ?? false,
			new_description = becameVisible.Changes?.NewDescription
		};

		/// <inheritdoc/>
		protected override object ProjectUpdate(AddonItemUpdate<SubAgentChange> update) => new
		{
			name = update.Name,
			description_changed = update.Changes?.DescriptionChanged ?? false,
			new_description = update.Changes?.NewDescription
		};
	}
}
