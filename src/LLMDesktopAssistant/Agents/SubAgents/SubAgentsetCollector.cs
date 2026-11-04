using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.LLM.Services;

namespace LLMDesktopAssistant.Agents.SubAgents
{
	/// <summary>
	/// The collector that builds the effective sub-agent addon set for the chat and its agents:
	/// file-system sub-agents (from addon packs and additional sources) plus prompt template sub-agents.
	/// </summary>
	[ChatService(typeof(IAddonSetCollector<SubAgentInfo>))]
	public class SubAgentsetCollector(
		IChatSettingsService chatSettings,
		IServiceProvider services
	) : AddonSetCollectorBase<SubAgentInfo, SubAgentChange>(services)
	{
		protected override AddonTarget ResolveAgentTarget(ChatAgentDescriptor agent)
			=> new(agent.SubAgents.GetEffectiveSubAgentset(chatSettings.Settings), agent.SubAgents);

		protected override void ApplyChange(SubAgentInfo target, SubAgentChange change, ChatAgentDescriptor? agent)
		{
			base.ApplyChange(target, change, agent);
			target.Model = change.Model ?? target.Model;
		}
	}
}
