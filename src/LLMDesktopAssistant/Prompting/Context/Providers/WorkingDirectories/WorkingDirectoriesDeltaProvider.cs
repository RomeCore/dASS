using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.LLM.Services.Prompting;

namespace LLMDesktopAssistant.Prompting.Context.Providers.WorkingDirectories
{
	/// <summary>
	/// Delta provider of the working directories section: diffs the current directories against the
	/// known ones (the anchor state with the already issued deltas applied) and reports
	/// the appearing section, the switched active directory, the added directories and the removed ones.
	/// </summary>
	[ChatService(typeof(IPromptSectionDeltaProvider<WorkingDirectoriesSectionState, WorkingDirectoriesSectionDelta>))]
	public class WorkingDirectoriesDeltaProvider(
		IPromptSectionStateProvider<WorkingDirectoriesSectionState> stateProvider
	) : IPromptSectionDeltaProvider<WorkingDirectoriesSectionState, WorkingDirectoriesSectionDelta>
	{
		/// <inheritdoc/>
		public WorkingDirectoriesSectionDelta? CalculateDelta(WorkingDirectoriesSectionState? anchorState,
			IEnumerable<WorkingDirectoriesSectionDelta> existingDeltas, EffectiveChatContext context)
		{
			var currentState = stateProvider.CaptureState(context.Agent);
			if (currentState is null)
				return null;

			var deltas = existingDeltas.ToList();

			// The section itself can be enabled after the anchor was created: the anchor then has
			// no state for it, so the whole list has to be announced at once.
			if (anchorState is null && !deltas.Any(delta => delta.Appeared))
			{
				return new WorkingDirectoriesSectionDelta
				{
					Appeared = true,
					Items = [.. currentState.Items]
				};
			}

			var knownItems = anchorState is null
				? new List<WorkingDirectoryItem>()
				: anchorState.Items.Select(item => item.Clone()).ToList();
			var knownActivePath = knownItems.FirstOrDefault(item => item.IsActive)?.Path;

			foreach (var delta in deltas)
				Apply(delta, knownItems, ref knownActivePath);

			var addedItems = currentState.Items
				.Where(current => !knownItems.Any(known => IsSameItem(known, current)))
				.ToList();
			var removedPaths = knownItems
				.Where(known => !currentState.Items.Any(current => IsSameItem(known, current)))
				.Select(known => known.Path)
				.ToList();

			var newActivePath = currentState.Items.FirstOrDefault(item => item.IsActive)?.Path;
			bool activeChanged = !WorkingDirectoriesStateProvider.IsSamePath(knownActivePath, newActivePath);

			if (addedItems.Count == 0 && removedPaths.Count == 0 && !activeChanged)
				return null;

			return new WorkingDirectoriesSectionDelta
			{
				ActiveChanged = activeChanged,
				OldActivePath = activeChanged ? knownActivePath : null,
				NewActivePath = activeChanged ? newActivePath : null,
				AddedItems = addedItems,
				RemovedPaths = removedPaths
			};
		}

		/// <summary>
		/// Applies an already issued delta to the known state, so that consecutive deltas of the same
		/// anchor are computed against the newest known state instead of the frozen anchor state.
		/// </summary>
		private static void Apply(WorkingDirectoriesSectionDelta delta, List<WorkingDirectoryItem> items,
			ref string? activePath)
		{
			if (delta.Appeared)
			{
				items.Clear();
				items.AddRange(delta.Items.Select(item => item.Clone()));
				activePath = items.FirstOrDefault(item => item.IsActive)?.Path;
				return;
			}

			items.RemoveAll(item => delta.RemovedPaths.Any(removed =>
				WorkingDirectoriesStateProvider.IsSamePath(removed, item.Path)));

			foreach (var added in delta.AddedItems)
				if (!items.Any(item => IsSameItem(item, added)))
					items.Add(added.Clone());

			if (delta.ActiveChanged)
				activePath = delta.NewActivePath;
		}

		/// <summary>
		/// The identity of a tracked directory: its name together with its path.
		/// </summary>
		private static bool IsSameItem(WorkingDirectoryItem left, WorkingDirectoryItem right)
			=> string.Equals(left.Name ?? string.Empty, right.Name ?? string.Empty, StringComparison.Ordinal)
				&& WorkingDirectoriesStateProvider.IsSamePath(left.Path, right.Path);
	}
}
