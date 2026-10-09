using LLMDesktopAssistant.Localization;

namespace LLMDesktopAssistant.InputCompletion
{
	/// <summary>
	/// One informational row of the completion popup's context block: something the caret's region declares but that
	/// cannot be completed — an argument of the command, say. Shown next to the picker, never selected, never inserted.
	/// </summary>
	public sealed class InputCompletionContextItem
	{
		/// <summary>
		/// The locale key of the row's name.
		/// </summary>
		public required LocaleKeyBase Name { get; init; }

		/// <summary>
		/// The locale key of the row's description, if any.
		/// </summary>
		public LocaleKeyBase? Description { get; init; }

		/// <summary>
		/// Whether the region requires the row (an argument that must be supplied for the command to run).
		/// </summary>
		public bool IsRequired { get; init; }

		/// <summary>
		/// Whether the row is the one the caret currently sits in.
		/// </summary>
		public bool IsCurrent { get; init; }
	}
}
