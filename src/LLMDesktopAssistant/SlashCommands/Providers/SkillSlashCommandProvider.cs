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

		/// <remarks>
		/// A skill substitutes the whole argument text into its body, so its schema is a single rest positional.
		/// </remarks>
		protected override SlashCommandArgumentSchema CreateArgumentSchema(SkillInfo source)
			=> new() { HasRestPositional = true };

		protected override ISlashCommandExecutor CreateCommandExecutor(SkillInfo source)
			=> StubCommandExecutor.Instance;
	}
}
