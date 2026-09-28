using LLMDesktopAssistant.Prompting.Context.AddonItems;

namespace LLMDesktopAssistant.Prompting.Context.Providers.Skills
{
	/// <summary>
	/// The serializable snapshot of a skill addon tracked by the skills section.
	/// </summary>
	public class SkillItem : AddonSectionItem
	{
		/// <summary>
		/// The path to the skill file (the location hint).
		/// </summary>
		public string? Path { get; set; }
	}
}
