using LLMDesktopAssistant.Controls.Icons;
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
		/// Extra text the popup shows with the item on hover, if any — for a command, its fully-qualified token when the
		/// item offers a shortened form.
		/// </summary>
		public string? Hint { get; init; }

		/// <summary>
		/// The kind of the item (used for the row icon).
		/// </summary>
		public InputCompletionKind Kind { get; init; }

		/// <summary>
		/// An explicit icon for the row, overriding the one the item's <see cref="Kind"/> would pick. Set this when a
		/// source wants a continuation to read differently than its category (for example, a tool-specific icon for a
		/// command it derived). <see langword="null"/> keeps the kind-based default.
		/// </summary>
		public VisualIconKind? IconOverride { get; init; }

		/// <summary>
		/// Whether the item is shadowed by a higher-priority one and is reachable only through a qualifier. Marked in
		/// the popup; it does not affect acceptance.
		/// </summary>
		public bool IsDefeated { get; init; }

		/// <summary>The display text, falling back to <see cref="InsertText"/>.</summary>
		public string DisplayText => Display ?? InsertText;
	}
}
