using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.Prompting.Context.AddonItems;

namespace LLMDesktopAssistant.Prompting.Context.Providers.SubAgents
{
	/// <summary>
	/// Delta provider of the sub-agents section: the common addon delta engine plus the
	/// description comparison.
	/// </summary>
	[ChatService(typeof(IPromptSectionDeltaProvider<SubAgentsSectionState, SubAgentsSectionDelta>))]
	public class SubAgentsDeltaProvider(
		IPromptSectionStateProvider<SubAgentsSectionState> stateProvider
	) : AddonSectionDeltaProvider<SubAgentsSectionState, SubAgentItem, SubAgentChange, SubAgentsSectionDelta>(stateProvider)
	{
		/// <inheritdoc/>
		protected override SubAgentChange? Diff(SubAgentItem known, SubAgentItem current)
		{
			bool descriptionChanged = known.Description != current.Description;
			if (!descriptionChanged)
				return null;

			return new SubAgentChange
			{
				DescriptionChanged = true,
				NewDescription = current.Description
			};
		}
	}
}
