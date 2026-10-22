using LLMDesktopAssistant.Agents;
using LLMDesktopAssistant.Prompting.Context;

namespace LLMDesktopAssistant.LLM.Services.Prompting
{
	/// <summary>
	/// The anchored SCM stage: manages the prompt state anchors of an agent (hybrid prompt mode only).
	/// Called by the prompt composer between the effective messages provider and message conversion.
	/// </summary>
	public interface IPromptAnchoredSectionProcessor
	{
		/// <summary>
		/// Returns the live anchor of the agent for the given effective context,
		/// creating or rebaselining it when needed.
		/// </summary>
		/// <param name="agent">The agent to process.</param>
		/// <param name="effectiveContext">The effective chat context of the agent.</param>
		/// <param name="sections">The anchored sections of the prompt context.</param>
		/// <param name="cancellationToken">The cancellation token forwarded to the sections.</param>
		/// <returns>The active anchor, or null outside of hybrid mode.</returns>
		Task<PromptStateAnchorMessageData?> ProcessAsync(ChatAgentDescriptor agent,
			EffectiveChatContext effectiveContext, IEnumerable<IPromptAnchoredSectionProvider> sections,
			CancellationToken cancellationToken = default);
	}
}
