using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.LLM.Services.Prompting;
using LLMDesktopAssistant.Utils.Files;

namespace LLMDesktopAssistant.Prompting.Context.Providers.SystemSlot
{
	/// <summary>
	/// Delta provider of the system slot section: diffs the current rendered text
	/// against the known one (anchor state + previously issued deltas).
	/// </summary>
	[ChatService(typeof(IPromptSectionDeltaProvider<SystemSlotSectionState, SystemSlotSectionDelta>))]
	public class SystemSlotDeltaProvider(
		IPromptSectionStateProvider<SystemSlotSectionState> stateProvider
	) : IPromptSectionDeltaProvider<SystemSlotSectionState, SystemSlotSectionDelta>
	{
		/// <inheritdoc/>
		public SystemSlotSectionDelta? CalculateDelta(SystemSlotSectionState? anchorState,
			IEnumerable<SystemSlotSectionDelta> existingDeltas, EffectiveChatContext context)
		{
			var knownText = anchorState?.Text ?? string.Empty;
			foreach (var delta in existingDeltas)
				knownText = delta.Diff.ApplyToText(knownText);

			var currentState = stateProvider.CaptureState(context.Agent);
			if (currentState is null)
				return null;

			var diff = UnifiedDiff.Compute(knownText, currentState.Text);
			if (!diff.HasGroups)
				return null;

			return new SystemSlotSectionDelta
			{
				Groups = diff.Groups
			};
		}
	}
}
