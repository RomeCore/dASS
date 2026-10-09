namespace LLMDesktopAssistant.InputCompletion
{
	/// <summary>
	/// The result of applying a continuation: the raw text after the edit and where the caret lands.
	/// </summary>
	public readonly record struct InputCompletionAccept(string Text, int Caret);
}
