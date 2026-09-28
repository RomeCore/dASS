using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.Prompting.Context.AddonItems;

namespace LLMDesktopAssistant.Prompting.Context.Providers.Skills
{
	/// <summary>
	/// Delta renderer of the skills section: projects the transitions into the
	/// 'skills_system_section_delta' template.
	/// </summary>
	[ChatService(typeof(IPromptSectionDeltaRenderer<SkillsSectionDelta>))]
	public class SkillsDeltaRenderer(
		ITemplateLibraryAccessor templates
	) : AddonSectionDeltaRenderer<SkillItem, SkillChange, SkillsSectionDelta>(templates)
	{
		/// <inheritdoc/>
		protected override string TemplateId => "skills_system_section_delta";

		/// <inheritdoc/>
		protected override object ProjectAddition(AddonItemAddition<SkillItem, SkillChange> addition) => new
		{
			name = addition.Name,
			hidden = addition.Hidden,
			has_definition = addition.Definition is not null,
			without_definition = !addition.Hidden && addition.Definition is null,
			definition_name = addition.Definition?.Name,
			definition_description = addition.Definition?.Description,
			definition_path = addition.Definition?.Path,
			definition_body = addition.Definition?.Body,
			description_changed = addition.Changes?.DescriptionChanged ?? false,
			new_description = addition.Changes?.NewDescription,
			path_changed = addition.Changes?.PathChanged ?? false,
			new_path = addition.Changes?.NewPath,
			body_changed = addition.Changes?.BodyChanged ?? false,
			new_body = addition.Changes?.NewBody
		};

		/// <inheritdoc/>
		protected override object ProjectRemoval(AddonItemRemoval removal) => new
		{
			name = removal.Name
		};

		/// <inheritdoc/>
		protected override object ProjectBecameVisible(AddonItemBecameVisible<SkillItem, SkillChange> becameVisible) => new
		{
			name = becameVisible.Name,
			has_definition = becameVisible.Definition is not null,
			without_definition = becameVisible.Definition is null,
			definition_name = becameVisible.Definition?.Name,
			definition_description = becameVisible.Definition?.Description,
			definition_path = becameVisible.Definition?.Path,
			definition_body = becameVisible.Definition?.Body,
			description_changed = becameVisible.Changes?.DescriptionChanged ?? false,
			new_description = becameVisible.Changes?.NewDescription,
			path_changed = becameVisible.Changes?.PathChanged ?? false,
			new_path = becameVisible.Changes?.NewPath,
			body_changed = becameVisible.Changes?.BodyChanged ?? false,
			new_body = becameVisible.Changes?.NewBody
		};

		/// <inheritdoc/>
		protected override object ProjectUpdate(AddonItemUpdate<SkillChange> update) => new
		{
			name = update.Name,
			description_changed = update.Changes?.DescriptionChanged ?? false,
			new_description = update.Changes?.NewDescription,
			path_changed = update.Changes?.PathChanged ?? false,
			new_path = update.Changes?.NewPath,
			body_changed = update.Changes?.BodyChanged ?? false,
			new_body = update.Changes?.NewBody
		};
	}
}
