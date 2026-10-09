using System.Collections.Immutable;
using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Agents.SubAgents;
using LLMDesktopAssistant.Agents.Tasks;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.LLM.Services.Agents;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.SlashCommands.Arguments;
using LLMDesktopAssistant.SlashCommands.Execution;

namespace LLMDesktopAssistant.SlashCommands.Providers
{
	/// <summary>
	/// Derives an <c>/agent:&lt;name&gt;</c> command from every available sub-agent.
	/// </summary>
	[ChatService(typeof(ISlashCommandProvider))]
	public class SubAgentSlashCommandProvider(
		IAddonSetCollector<SubAgentInfo> sources,
		ISubAgentTaskParamsResolver paramsResolver,
		IAgentTaskExecutor agentTaskExecutor,
		IChatSettingsService chatSettings)
		: DerivedSlashCommandProvider<SubAgentInfo>(sources)
	{
		protected override string TypeNamespace => "agent";

		protected override SlashCommandSource SourceKind => SlashCommandSource.SubAgent;

		protected override ISlashCommandExecutor CreateCommandExecutor(SubAgentInfo source)
			=> new SubAgentCommandExecutor(source, paramsResolver, agentTaskExecutor, chatSettings);
	}
}
