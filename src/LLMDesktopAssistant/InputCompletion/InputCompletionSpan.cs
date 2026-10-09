namespace LLMDesktopAssistant.InputCompletion
{
	/// <summary>
	/// A half-open region <c>[Start, End)</c> of the raw input text. The region a completion would replace is defined
	/// by the source that produced it — never inferred from whitespace, so a token may contain spaces (a mention such
	/// as <c>@Code Reviewer</c>, say).
	/// </summary>
	public readonly record struct InputCompletionSpan(int Start, int Length)
	{
		/// <summary>The exclusive end of the span.</summary>
		public int End => Start + Length;

		/// <summary>Creates the smallest span that covers both spans.</summary>
		public static InputCompletionSpan FromBounds(int start, int end) => new(start, end - start);
	}
}
