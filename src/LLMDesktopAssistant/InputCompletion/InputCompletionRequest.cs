namespace LLMDesktopAssistant.InputCompletion
{
	/// <summary>
	/// A request for an input completion: the whole raw input text and the caret position inside it.
	/// </summary>
	/// <remarks>
	/// The text is the raw box content (not a single line); a source decides for itself which region around the caret
	/// it claims. <see cref="CaretIndex"/> is a raw-text offset in <c>[0, Text.Length]</c>.
	/// </remarks>
	public readonly record struct InputCompletionRequest(string Text, int CaretIndex);
}
