namespace LLMDesktopAssistant.Controls.Text;

/// <summary>
/// An inline completion a <see cref="HighlightTextBox"/> previews and accepts character by character with the Right
/// key. The provider owns the completion text; the control owns the text edit and the caret.
/// </summary>
public interface IInlineCompletionProvider
{
	/// <summary>
	/// The characters to insert at the caret. <see langword="null"/> or empty means there is no inline completion.
	/// </summary>
	string? CompletionText { get; }

	/// <summary>
	/// The absolute offset in the raw text where the token being completed ends. On Right, one character in
	/// <c>[caret, TokenEnd)</c> is replaced by one character of <see cref="CompletionText"/>; nothing is consumed when
	/// the caret is at or past <see cref="TokenEnd"/>.
	/// </summary>
	int TokenEnd { get; }
}
