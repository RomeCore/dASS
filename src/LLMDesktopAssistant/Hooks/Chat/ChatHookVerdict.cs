namespace LLMDesktopAssistant.Hooks.Chat
{
	/// <summary>
	/// What a gating chat hook returns: its opinion and, optionally, the reason the user is shown.
	/// </summary>
	/// <remarks>
	/// The verdict is a chain value, and the chain replaces rather than merges: each sequential hook gets the previous
	/// verdict as <c>previous</c> and its return value becomes the new one in full. A hook that has nothing to add must
	/// therefore return <c>previous</c> untouched, or nothing at all, which means "no opinion".
	/// </remarks>
	public sealed class ChatHookVerdict
	{
		/// <summary>
		/// The hook's opinion, or <see langword="null"/> for "no opinion".
		/// </summary>
		public ChatHookDecision? Decision { get; init; }

		/// <summary>
		/// Why the hook refused. It reaches the user as the toast text when the pipeline has a surface for it, and the
		/// log otherwise. Ignored when the decision is not a refusal.
		/// </summary>
		public string? Reason { get; init; }
	}
}
