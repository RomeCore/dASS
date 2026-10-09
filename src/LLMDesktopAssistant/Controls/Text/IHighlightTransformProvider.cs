namespace LLMDesktopAssistant.Controls.Text;

/// <summary>
/// Provides an optional rendered-text transform and/or highlight spans for <see cref="HighlightTextBox"/>.
/// Replaces the separate DisplayTextTransform and HighlightProvider delegates with a single contract.
/// </summary>
/// <remarks>
/// <see cref="Transform"/> is invoked automatically on every text change (including IME preedit).
/// Raise <see cref="LayoutChanged"/> when the visual output may change independently of the text
/// (e.g. a highlight toggle was flipped or a color scheme was reloaded), so the control can
/// recompute the layout without touching the text.
/// </remarks>
public interface IHighlightTransformProvider
{
	/// <summary>
	/// Raised when the transform output should be recomputed even though the text itself did not change.
	/// </summary>
	event EventHandler? LayoutChanged;

	/// <summary>
	/// Computes the rendered text and/or highlight spans for the specified text and caret.
	/// </summary>
	/// <param name="text">The current (raw, untransformed) text, IME preedit included.</param>
	/// <param name="caretIndex">The caret position in the text: a provider paints the region the caret is in.</param>
	HighlightTransformResult Transform(string text, int caretIndex);
}
