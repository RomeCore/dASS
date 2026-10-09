using System.Collections.Generic;

namespace LLMDesktopAssistant.InputCompletion
{
	/// <summary>
	/// The completion a source computed for a request: the region to replace, the current state and the continuations.
	/// </summary>
	public sealed class InputCompletionResult
	{
		/// <summary>
		/// The raw-text region the accept action replaces. Defined by the source; for a partially typed token it covers
		/// the typed token, so accepting mid-token replaces the tail too.
		/// </summary>
		public required InputCompletionSpan Span { get; init; }

		/// <summary>
		/// What the caret is currently inside. May be non-null while <see cref="Items"/> is empty.
		/// </summary>
		public InputCompletionState? State { get; init; }

		/// <summary>
		/// The continuations, possibly empty.
		/// </summary>
		public IReadOnlyList<InputCompletionItem> Items { get; init; } = [];

		/// <summary>
		/// The index of the item selected in the popup.
		/// </summary>
		public int SelectedIndex { get; init; }

		/// <summary>
		/// The text the renderer shows as a ghost preview after the input (a completion suffix, an argument hint, ...).
		/// The source computes the text; the renderer decides how to draw it. <see langword="null"/> when there is
		/// nothing to preview.
		/// </summary>
		public string? GhostText { get; init; }
	}
}
