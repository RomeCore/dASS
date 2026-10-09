using System.Diagnostics.CodeAnalysis;
using LLMDesktopAssistant.Controls.Text;

namespace LLMDesktopAssistant.InputCompletion
{
	/// <summary>
	/// One source of input completions — a slash-command matcher, a chat-agent-mention matcher, a file-path provider.
	/// A source is deliberately generic: it does not know what the UI does with the result.
	/// </summary>
	/// <remarks>
	/// Sources are registered in the chat scope. For a given caret the service picks the single source with the
	/// highest <see cref="Priority"/> that claims it (see <see cref="IInputCompletionService"/>), so a source must
	/// return <see langword="false"/> — not an empty result — when the caret is not in a region it owns.
	/// </remarks>
	public interface IInputCompletionSource
	{
		/// <summary>
		/// The source's priority; the higher value wins when several sources claim the caret.
		/// </summary>
		int Priority { get; }

		/// <summary>
		/// Tries to compute a completion for the request.
		/// </summary>
		/// <param name="text">The text to complete.</param>
		/// <param name="caretIndex">The caret position in the text.</param>
		/// <returns>The computed completion when the source claims the request, or <see langword="null"/> otherwise.</returns>
		InputCompletionResult? TryCompute(string text, int caretIndex);

		/// <summary>
		/// Tries to highlight the request. Returns <see langword="null"/> when the source does not own the caret.
		/// </summary>
		/// <param name="text">The text to complete.</param>
		/// <param name="caretIndex">The caret position in the text.</param>
		/// <returns>The computed highlights when the source claims the request, or <see langword="null"/> otherwise.</returns>
		IReadOnlyList<TextHighlightSpan>? TryHighlight(string text, int caretIndex);
	}
}
