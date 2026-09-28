using LLMDesktopAssistant.LLM.Services;

namespace LLMDesktopAssistant.Prompting.Context.Providers.Skills
{
	/// <summary>
	/// Renders the skills section state into a snapshot (text only).
	/// </summary>
	[ChatService(typeof(IPromptSectionStateRenderer<SkillsSectionState>))]
	public class SkillsStateRenderer(
		ITemplateLibraryAccessor templates
	) : IPromptSectionStateRenderer<SkillsSectionState>
	{
		/// <inheritdoc/>
		public SystemPromptSnapshot Render(SkillsSectionState state)
		{
			return templates.GetTextTemplate("skills_system_section").Render(new
			{
				skills = state.Items.Count > 0
					? state.Items.Select(s => new
					{
						name = s.Name,
						description = s.Description,
						path = s.Path,
						body = s.Body
					}).ToArray()
					: null,
				hidden_skills = state.HiddenNames.Count > 0 ? state.HiddenNames.ToArray() : null
			});
		}
	}
}
