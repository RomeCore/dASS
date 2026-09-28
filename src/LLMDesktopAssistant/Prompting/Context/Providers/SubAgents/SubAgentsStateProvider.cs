using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Agents;
using LLMDesktopAssistant.Agents.SubAgents;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.LLM.Services.Prompting;

namespace LLMDesktopAssistant.Prompting.Context.Providers.SubAgents
{
	/// <summary>
	/// Captures the sub-agents section state by collecting the sub-agents of the agent.
	/// The hidden names are captured in the hybrid mode only.
	/// </summary>
	[ChatService(typeof(IPromptSectionStateProvider<SubAgentsSectionState>))]
	public class SubAgentsStateProvider(
		IAddonSetCollector<SubAgentInfo> subAgentsetCollector
		) : IPromptSectionStateProvider<SubAgentsSectionState>
	{
		/// <inheritdoc/>
		public SubAgentsSectionState CaptureState(ChatAgentDescriptor agent)
		{
			var items = new List<SubAgentItem>();
			var hiddenNames = new List<string>();

			bool captureHiddenSubAgents = agent.Context.PromptMode == PromptContextMode.Hybrid;

			foreach (var subAgent in subAgentsetCollector.GetAddonsForAgent(agent))
			{
				if (subAgent.Hidden ?? false)
				{
					if (captureHiddenSubAgents)
						hiddenNames.Add(subAgent.Name);
				}
				else
				{
					items.Add(new SubAgentItem
					{
						Name = subAgent.Name,
						Description = subAgent.Description
					});
				}
			}

			return new SubAgentsSectionState
			{
				Items = [.. items.OrderBy(s => s.Name, StringComparer.Ordinal)],
				HiddenNames = [.. hiddenNames.OrderBy(n => n, StringComparer.Ordinal)]
			};
		}
	}
}
