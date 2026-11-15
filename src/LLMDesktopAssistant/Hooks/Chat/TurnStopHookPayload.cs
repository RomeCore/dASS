namespace LLMDesktopAssistant.Hooks.Chat
{
	/// <summary>
	/// The document a <c>chat.turn.stop.*</c> hook receives: the multi-agent turn is over.
	/// </summary>
	/// <remarks>
	/// Fired once per run of the turn, from the finally that every exit path passes through - a return because no
	/// agent was left, a follow-up barrier, a cancellation and a throw all end here.
	/// </remarks>
	public sealed class TurnStopHookPayload
	{
		/// <summary>
		/// The chat the turn belongs to, or <see langword="null"/> when the world has none.
		/// </summary>
		public int? ChatId { get; init; }

		/// <summary>
		/// Why the turn ended in kebab case: <c>complete</c>, <c>interrupted</c> or <c>cancelled</c>.
		/// </summary>
		public string Reason { get; init; } = string.Empty;

		/// <summary>
		/// The agents that ran during the turn, in the order they were selected.
		/// </summary>
		public ChatHookAgentInfo[] Agents { get; init; } = [];
	}
}
