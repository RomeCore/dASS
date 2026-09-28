using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.Prompting.Context.AddonItems;

namespace LLMDesktopAssistant.Prompting.Context.Providers.Skills
{
	/// <summary>
	/// Delta provider of the skills section: the common addon delta engine plus the skill
	/// field-level comparison (description, path and body).
	/// </summary>
	[ChatService(typeof(IPromptSectionDeltaProvider<SkillsSectionState, SkillsSectionDelta>))]
	public class SkillsDeltaProvider(
		IPromptSectionStateProvider<SkillsSectionState> stateProvider
	) : AddonSectionDeltaProvider<SkillsSectionState, SkillItem, SkillChange, SkillsSectionDelta>(stateProvider)
	{
		/// <inheritdoc/>
		protected override SkillChange? Diff(SkillItem known, SkillItem current)
		{
			bool descriptionChanged = known.Description != current.Description;
			bool pathChanged = known.Path != current.Path;
			bool bodyChanged = known.Body != current.Body;
			if (!descriptionChanged && !pathChanged && !bodyChanged)
				return null;

			return new SkillChange
			{
				DescriptionChanged = descriptionChanged,
				NewDescription = descriptionChanged ? current.Description : null,
				PathChanged = pathChanged,
				NewPath = pathChanged ? current.Path : null,
				BodyChanged = bodyChanged,
				NewBody = bodyChanged ? current.Body : null
			};
		}
	}
}
