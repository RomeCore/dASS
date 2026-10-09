using System.Collections.Generic;
using LLMDesktopAssistant.Localization;

namespace LLMDesktopAssistant.InputCompletion
{
	/// <summary>
	/// What the caret is currently inside, as the popup shows it — for example the command being typed, or a command
	/// plus the argument the caret sits in. A result may carry a state with an empty item list: the popup then renders
	/// the state alone (a hint, an error, a "no matches" note, the command's context).
	/// </summary>
	/// <remarks>
	/// The state is the popup's <b>presentation</b>; the continuations to accept live on the result (one picker only,
	/// because a single source claims a caret). It is laid out as a header (<see cref="Title"/>,
	/// <see cref="Description"/>) followed by an optional context block (<see cref="ContextTitle"/>,
	/// <see cref="ContextDescription"/> and <see cref="ContextItems"/>) and then the picker.
	/// </remarks>
	public sealed class InputCompletionState
	{
		/// <summary>
		/// The locale key of the state's title, if any.
		/// </summary>
		public LocaleKeyBase? Title { get; init; }

		/// <summary>
		/// The locale key of the state's description, if any.
		/// </summary>
		public LocaleKeyBase? Description { get; init; }

		/// <summary>
		/// The locale key of the context block's heading, if any — the argument the caret sits in, or the name of the
		/// list <see cref="ContextItems"/> carries.
		/// </summary>
		public LocaleKeyBase? ContextTitle { get; init; }

		/// <summary>
		/// The locale key of the context block's description, if any.
		/// </summary>
		public LocaleKeyBase? ContextDescription { get; init; }

		/// <summary>
		/// The context block's rows — what the caret's region declares but cannot complete. Informational: they are
		/// shown next to the picker and are never selected or accepted.
		/// </summary>
		public IReadOnlyList<InputCompletionContextItem> ContextItems { get; init; } = [];

		/// <summary>
		/// The kind of the state (used for a leading icon).
		/// </summary>
		public InputCompletionKind Kind { get; init; }
	}
}
