using LLMDesktopAssistant.LLM.Services.Prompting;

namespace LLMDesktopAssistant.Prompting.Context.AddonItems
{
	/// <summary>
	/// The base delta provider of an addon section: captures the current state, runs the common
	/// <see cref="AddonSectionDeltaEngine"/> and packs the result into the concrete section delta.
	/// The section only supplies the field-level comparison (<see cref="Diff"/>).
	/// </summary>
	public abstract class AddonSectionDeltaProvider<TState, TItem, TChange, TDelta>(
		IPromptSectionStateProvider<TState> stateProvider
	) : IPromptSectionDeltaProvider<TState, TDelta>
		where TState : AddonSectionState<TItem>
		where TItem : AddonSectionItem
		where TChange : AddonItemChange<TItem>
		where TDelta : AddonSectionDelta<TItem, TChange>, new()
	{
		/// <inheritdoc/>
		public TDelta? CalculateDelta(TState? anchorState,
			IEnumerable<TDelta> existingDeltas, EffectiveChatContext context)
		{
			var currentState = stateProvider.CaptureState(context.Agent);
			if (currentState is null)
				return null;

			var result = AddonSectionDeltaEngine.Compute(
				anchorState?.Items,
				anchorState?.HiddenNames,
				existingDeltas,
				currentState.Items,
				currentState.HiddenNames,
				Diff);
			if (result is null)
				return null;

			return new TDelta
			{
				AddedItems = result.AddedItems,
				RemovedItems = result.RemovedItems,
				BecameVisibleItems = result.BecameVisibleItems,
				UpdatedItems = result.UpdatedItems
			};
		}

		/// <summary>
		/// Compares the last known item with the current one and returns the field-level changes,
		/// or null when nothing has changed. This is the only per-section delta logic.
		/// </summary>
		protected abstract TChange? Diff(TItem known, TItem current);
	}
}
