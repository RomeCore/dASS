using System.Diagnostics.CodeAnalysis;

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
		/// Tries to compute a completion for the request. Returns <see langword="false"/> when the source does not own
		/// the caret (it must not return an empty result in that case).
		/// </summary>
		/// <param name="request">The text and the caret position.</param>
		/// <param name="result">The computed completion when the source claims the request.</param>
		bool TryCompute(InputCompletionRequest request, [NotNullWhen(true)] out InputCompletionResult? result);
	}
}
