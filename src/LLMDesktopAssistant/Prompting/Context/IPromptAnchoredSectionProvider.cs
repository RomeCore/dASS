using LLMDesktopAssistant.Agents;
using LLMDesktopAssistant.LLM.Services.Prompting;

namespace LLMDesktopAssistant.Prompting.Context
{
	/// <summary>
	/// An anchored prompt section: a part of the system prompt that captures its own state
	/// and renders it into a text fragment and/or tool definitions.
	/// </summary>
	public interface IPromptAnchoredSectionProvider : IPromptContextProvider
	{
		/// <summary>
		/// Gets the discriminator used to separate the state and delta data from other prompt contexts.
		/// </summary>
		string Discriminator { get; }

		/// <summary>
		/// Captures the current, and initial state of this section for the given agent.
		/// </summary>
		Task<PromptSectionStateBase?> CaptureStateAsync(ChatAgentDescriptor agent,
			CancellationToken cancellationToken = default);

		/// <summary>
		/// Calculates the delta between the recorded state (anchor + deltas) and the actual state (should be calculated implicitly).
		/// </summary>
		/// <returns>The calculated delta, or null if no changes were detected.</returns>
		Task<PromptSectionDeltaBase?> CalculateDeltaAsync(PromptSectionStateBase? anchorState,
			IEnumerable<PromptSectionDeltaBase> existingDeltas, EffectiveChatContext context,
			CancellationToken cancellationToken = default);

		/// <summary>
		/// Renders the captured state into a system prompt snapshot fragment (text and/or tools).
		/// </summary>
		SystemPromptSnapshot RenderState(PromptSectionStateBase state);

		/// <summary>
		/// Renders a delta into a compact text fragment.
		/// </summary>
		string RenderDelta(PromptSectionDeltaBase delta);
	}
}
