using System;
using System.Collections.Generic;

namespace LLMDesktopAssistant.InputCompletion
{
	/// <summary>
	/// The completion a source computed for a request: the region to replace, the current state and the continuations.
	/// A result is a pure snapshot of that computation — the accepted edit is derived from it, never stored on it.
	/// </summary>
	public sealed class InputCompletionResult
	{
		/// <summary>
		/// The raw input text the continuations were computed for.
		/// </summary>
		public required string Text { get; init; }

		/// <summary>
		/// The caret position in <see cref="Text"/> the continuations were computed for.
		/// </summary>
		public required int CaretIndex { get; init; }

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

		/// <summary>The continuation at <paramref name="index"/>, or <see langword="null"/> when there is none.</summary>
		public InputCompletionItem? ItemAt(int index)
			=> index >= 0 && index < Items.Count ? Items[index] : null;

		/// <summary>
		/// The suffix of <paramref name="item"/>'s insertion the text before <see cref="CaretIndex"/> does not carry yet
		/// — the ghost the renderer previews — or <see langword="null"/> when there is nothing to preview: the item does
		/// not <i>continue</i> what is typed (a command reached through an alias, say), the whole insertion is already
		/// there, or the caret sits outside the region.
		/// </summary>
		public string? GhostOf(InputCompletionItem item)
		{
			var start = Math.Clamp(Span.Start, 0, Text.Length);
			var end = Math.Clamp(Span.End, start, Text.Length);
			var caret = Math.Clamp(CaretIndex, start, end);

			var typed = caret - start;
			var insert = item.InsertText;
			if (typed > insert.Length)
				return null;

			if (!insert.AsSpan(0, typed).Equals(Text.AsSpan(start, typed), StringComparison.OrdinalIgnoreCase))
				return null;

			return typed < insert.Length ? insert[typed..] : null;
		}

		/// <summary>
		/// Applies <paramref name="item"/> to <see cref="Text"/>. With <paramref name="oneChar"/> it commits a single
		/// character of the ghost at the caret (the Right-key inline accept), consuming the character it overwrote;
		/// otherwise it replaces the whole <see cref="Span"/> with the insertion plus a trailing space. Returns
		/// <see langword="null"/> when there is nothing to apply.
		/// </summary>
		public InputCompletionAccept? Apply(InputCompletionItem item, bool oneChar)
		{
			var start = Math.Clamp(Span.Start, 0, Text.Length);
			var end = Math.Clamp(Span.End, start, Text.Length);
			var caret = Math.Clamp(CaretIndex, 0, Text.Length);

			if (!oneChar)
			{
				var replacement = item.InsertText;
				var endText = Text[end..];
				if (endText.Length == 0 || !char.IsWhiteSpace(endText[0]))
					replacement = replacement + " ";
				return new InputCompletionAccept(Text[..start] + replacement + endText, start + replacement.Length);
			}

			if (GhostOf(item) is not { Length: > 0 } ghost)
				return null;

			// Consume the character under the caret only while it is still inside the region (the token tail).
			var consume = caret < end ? 1 : 0;
			return new InputCompletionAccept(Text[..caret] + ghost[0] + Text[(caret + consume)..], caret + 1);
		}
	}
}
