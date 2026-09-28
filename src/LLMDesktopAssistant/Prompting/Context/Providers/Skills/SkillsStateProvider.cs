using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Agents;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.LLM.Services.Prompting;
using LLMDesktopAssistant.Prompting.Skills;

namespace LLMDesktopAssistant.Prompting.Context.Providers.Skills
{
	/// <summary>
	/// Captures the skills section state by collecting the skills of the agent.
	/// The hidden names are captured in the hybrid mode only.
	/// </summary>
	[ChatService(typeof(IPromptSectionStateProvider<SkillsSectionState>))]
	public class SkillsStateProvider(
		IAddonSetCollector<SkillInfo> skillsetBuilder
		) : IPromptSectionStateProvider<SkillsSectionState>
	{
		/// <inheritdoc/>
		public SkillsSectionState CaptureState(ChatAgentDescriptor agent)
		{
			var items = new List<SkillItem>();
			var hiddenNames = new List<string>();

			bool captureHiddenSkills = agent.Context.PromptMode == PromptContextMode.Hybrid;

			foreach (var skill in skillsetBuilder.GetAddonsForAgent(agent))
			{
				if (skill.Hidden ?? false)
				{
					if (captureHiddenSkills)
						hiddenNames.Add(skill.Name);
				}
				else
				{
					items.Add(new SkillItem
					{
						Name = skill.Name,
						Description = skill.Description,
						Path = skill.Path,
						Body = skill.InjectionMode is SkillInjectionMode.Full ? skill.BodyGetter(skill) : null
					});
				}
			}

			return new SkillsSectionState
			{
				Items = [.. items.OrderBy(s => s.Name, StringComparer.Ordinal)],
				HiddenNames = [.. hiddenNames.OrderBy(n => n, StringComparer.Ordinal)]
			};
		}
	}
}
