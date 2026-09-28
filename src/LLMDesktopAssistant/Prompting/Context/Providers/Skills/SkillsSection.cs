namespace LLMDesktopAssistant.Prompting.Context.Providers.Skills
{
	/// <summary>
	/// The skills section: provides the available skills for the system prompt.
	/// </summary>
	public class SkillsSection(IServiceProvider services)
		: PromptAnchoredSectionBase<SkillsSectionState, SkillsSectionDelta>(services)
	{
		public override string Discriminator => "skills";
	}
}
