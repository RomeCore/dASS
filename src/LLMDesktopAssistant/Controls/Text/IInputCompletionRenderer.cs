namespace LLMDesktopAssistant.Controls.Text;

/// <summary>
/// Paints the input region a completion source owns. A source implements it next to
/// <see cref="InputCompletion.IInputCompletionSource"/>, so what a feature completes and how it looks stay in one
/// place — the input view never learns which features exist.
/// </summary>
public interface IInputCompletionRenderer
{
	/// <summary>
	/// The spans (and, if any, the rendered text) for the region the caret is in, or <see langword="null"/> when the
	/// caret is not in a region this renderer owns.
	/// </summary>
	/// <param name="text">The current (raw, untransformed) text, IME preedit included.</param>
	/// <param name="caretIndex">The caret position in the text.</param>
	HighlightTransformResult? Render(string text, int caretIndex);
}
