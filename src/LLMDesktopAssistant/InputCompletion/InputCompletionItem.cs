using LLMDesktopAssistant.Localization;

namespace LLMDesktopAssistant.InputCompletion
{
	/// <summary>
	/// A single continuation offered by an input-completion source.
	/// </summary>
	public sealed class InputCompletionItem
	{
		/// <summary>
		/// The text that replaces the result's span when the item is accepted.
		/// </summary>
		public required string InsertText { get; init; }

		/// <summary>
		/// The text shown in the completion list. Falls back to <see cref="InsertText"/> when not set.
		/// </summary>
		public string? Display { get; init; }

		/// <summary>
		/// The locale key of the item's description, if any.
		/// </summary>
		public LocaleKeyBase? Description { get; init; }

		/// <summary>
		/// The kind of the item (used for the row icon).
		/// </summary>
		public InputCompletionKind Kind { get; init; }

		/// <summary>
		/// Whether the item is shadowed by a higher-priority one and is reachable only through a qualifier. Marked in
		/// the popup; it does not affect acceptance.
		/// </summary>
		public bool IsDefeated { get; init; }

		/// <summary>The display text, falling back to <see cref="InsertText"/>.</summary>
		public string DisplayText => Display ?? InsertText;
	}
}
