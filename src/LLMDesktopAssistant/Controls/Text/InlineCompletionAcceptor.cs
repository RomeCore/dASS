using System;

namespace LLMDesktopAssistant.Controls.Text;

/// <summary>
/// The pure text edit behind the Right-key inline completion: it consumes one character of the token being completed
/// and commits one character of the completion at the caret. Extracted so it is unit-testable without the control.
/// </summary>
public static class InlineCompletionAcceptor
{
	/// <summary>
	/// Accepts one character of <paramref name="completion"/> at <paramref name="caretIndex"/>: the character is
	/// inserted, and one character of the token tail <c>[caretIndex, tokenEnd)</c> is consumed when there is one.
	/// </summary>
	/// <param name="text">The current raw text.</param>
	/// <param name="caretIndex">The caret position in <paramref name="text"/>.</param>
	/// <param name="completion">The completion text to commit one character of.</param>
	/// <param name="tokenEnd">The absolute end of the token being completed.</param>
	/// <returns>The new text and caret, or <see langword="null"/> when there is nothing to accept.</returns>
	public static (string Text, int Caret)? Accept(string? text, int caretIndex, string? completion, int tokenEnd)
	{
		text ??= string.Empty;

		if (string.IsNullOrEmpty(completion))
			return null;

		caretIndex = Math.Clamp(caretIndex, 0, text.Length);

		// Consume the character under the caret only when it is still inside the token.
		var consume = caretIndex < tokenEnd && caretIndex < text.Length ? 1 : 0;
		var accepted = text[..caretIndex] + completion[0] + text[(caretIndex + consume)..];

		return (accepted, caretIndex + 1);
	}
}
