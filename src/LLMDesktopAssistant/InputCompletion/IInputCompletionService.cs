using System;

namespace LLMDesktopAssistant.InputCompletion
{
	/// <summary>
	/// The input's completion session: it resolves the single source that owns the caret and keeps the result, so the
	/// popup and the renderer of the input see one coherent completion.
	/// </summary>
	public interface IInputCompletionService
	{
		/// <summary>Raised when <see cref="Result"/> changed.</summary>
		event EventHandler? ResultChanged;

		/// <summary>
		/// The completion for the last <see cref="Update"/>, or <see langword="null"/> when nothing claims the caret or
		/// the completion was closed.
		/// </summary>
		InputCompletionResult? Result { get; }

		/// <summary>
		/// Recomputes the completion for the text and the caret, from the highest-priority source that claims them.
		/// </summary>
		void Update(string? text, int caretIndex);

		/// <summary>
		/// Drops the current completion without recomputing — the user closed it, or the caret was moved by hand.
		/// </summary>
		void Close();
	}
}
