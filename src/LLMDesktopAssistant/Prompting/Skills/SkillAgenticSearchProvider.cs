using System.Text;
using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Addons.Search;
using LLMDesktopAssistant.LLM.Services;

namespace LLMDesktopAssistant.Prompting.Skills
{
	/// <summary>
	/// The addon tools provider for skills: renders the skill name, description and, in the detailed
	/// mode, the common addon metadata. The parameter schema of template skills is intentionally not
	/// rendered - it is a UI concern and is not usable by the model.
	/// </summary>
	[ChatService(typeof(IAddonAgenticSearchProvider))]
	public class SkillAgenticSearchProvider(
		IAddonSetCollector<SkillInfo> collector,
		IAddonSearchService<SkillInfo> searchService)
		: AddonAgenticSearchProvider<SkillInfo>(collector, searchService)
	{
		/// <inheritdoc/>
		public override AddonKind Kind => AddonKind.Skill;

		/// <inheritdoc/>
		public override string Title => "Skills";

		/// <inheritdoc/>
		public override string UsageHint => "load with `skill-load` by name";

		/// <inheritdoc/>
		protected override void AppendAddon(StringBuilder builder, SkillInfo skill, bool detailed)
		{
			AddonSearchFormatting.AppendItem(builder, skill.Name, skill.Description);

			if (!detailed)
				return;

			AddonSearchFormatting.AppendMetadata(builder, skill.Tags, skill.SourcePack?.Name, skill.Path);
		}
	}
}
