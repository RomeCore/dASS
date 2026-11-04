using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Agents;
using LLMDesktopAssistant.LLM.Services;

namespace LLMDesktopAssistant.Prompting.Skills
{
	[ChatService(typeof(IAddonSetCollector<SkillInfo>))]
	public class SkillsetCollector(
		IChatSettingsService chatSettings,
		IServiceProvider services
	) : AddonSetCollectorBase<SkillInfo, SkillChange>(services)
	{
		protected override AddonTarget ResolveAgentTarget(ChatAgentDescriptor agent)
			=> new(agent.Skills.GetEffectiveSkillset(chatSettings.Settings), agent.Skills);

		protected override void ApplyChange(SkillInfo target, SkillChange change, ChatAgentDescriptor? agent)
		{
			base.ApplyChange(target, change, agent);
			target.InjectionMode = change.InjectionMode ?? target.InjectionMode;
		}
	}
}
