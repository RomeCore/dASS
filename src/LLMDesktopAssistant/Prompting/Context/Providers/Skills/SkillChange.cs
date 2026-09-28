using LLMDesktopAssistant.Prompting.Context.AddonItems;

namespace LLMDesktopAssistant.Prompting.Context.Providers.Skills
{
	/// <summary>
	/// The field-level description of a skill change: the common description/body fields live
	/// in the base, the path is skill-specific.
	/// </summary>
	public class SkillChange : AddonItemChange<SkillItem>
	{
		/// <summary>
		/// Whether the skill path has changed.
		/// </summary>
		public bool PathChanged { get; set; }

		/// <summary>
		/// The new skill path (when <see cref="PathChanged"/> is true).
		/// </summary>
		public string? NewPath { get; set; }

		protected override void ApplyExtra(SkillItem item)
		{
			if (PathChanged)
				item.Path = NewPath;
		}
	}
}
