using LLMDesktopAssistant.LLM.Services.Prompting;

namespace LLMDesktopAssistant.Prompting.Context
{
	public interface IPromptSectionDeltaProvider<TState, TDelta>
		where TState : PromptSectionStateBase
		where TDelta : PromptSectionDeltaBase
	{
		/// <summary>
		/// Calculates the delta between recorded state (anchor + deltas) and the actual state.
		/// </summary>
		/// <param name="anchorState">The anchor state to compare against. This is the state before any deltas were applied.</param>
		/// <param name="existingDeltas">The existing deltas to apply to the anchor state to get the current state.</param>
		/// <param name="context">The effective chat context to consider when calculating the delta.</param>
		/// <param name="cancellationToken">The cancellation token forwarded to the state provider.</param>
		/// <returns>The calculated delta, or null if no changes were detected.</returns>
		Task<TDelta?> CalculateDeltaAsync(TState? anchorState, IEnumerable<TDelta> existingDeltas,
			EffectiveChatContext context, CancellationToken cancellationToken = default);
	}
}
