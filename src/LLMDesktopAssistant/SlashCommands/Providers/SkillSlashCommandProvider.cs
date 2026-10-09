using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.Prompting.Skills;
using LLMDesktopAssistant.SlashCommands.Arguments;
using LLMDesktopAssistant.SlashCommands.Execution;

namespace LLMDesktopAssistant.SlashCommands.Providers
{
	/// <summary>
	/// Derives a <c>/skill:&lt;name&gt;</c> command from every available skill.
	/// </summary>
	[ChatService(typeof(ISlashCommandProvider))]
	public class SkillSlashCommandProvider(IAddonSetCollector<SkillInfo> sources)
		: DerivedSlashCommandProvider<SkillInfo>(sources)
	{
		protected override string TypeNamespace => "skill";

		protected override SlashCommandSource SourceKind => SlashCommandSource.Skill;

		protected override ISlashCommandExecutor CreateCommandExecutor(SkillInfo source)
			=> new SkillCommandExecutor(source);
	}
}
