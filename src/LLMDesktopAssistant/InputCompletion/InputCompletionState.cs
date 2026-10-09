using LLMDesktopAssistant.Localization;

namespace LLMDesktopAssistant.InputCompletion
{
	/// <summary>
	/// What the caret is currently inside, as the popup shows it — for example "command", "argument: wait" or the
	/// typed-but-unknown token. A result may carry a state with an empty item list: the popup then renders the state
	/// alone (a hint, an error, a "no matches" note).
	/// </summary>
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
		/// The kind of the state (used for a leading icon).
		/// </summary>
		public InputCompletionKind Kind { get; init; }
	}
}
