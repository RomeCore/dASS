namespace LLMDesktopAssistant.InputCompletion
{
	/// <summary>
	/// Resolves the single input-completion source that owns the caret, so the input has one coherent completion at a
	/// time.
	/// </summary>
	public interface IInputCompletionService
	{
		/// <summary>
		/// Computes the completion for <paramref name="request"/> from the highest-priority source that claims it, or
		/// returns <see langword="null"/> when no source does.
		/// </summary>
		InputCompletionResult? Compute(InputCompletionRequest request);
	}
}
