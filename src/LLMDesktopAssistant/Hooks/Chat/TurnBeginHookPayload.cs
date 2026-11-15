namespace LLMDesktopAssistant.Hooks.Chat
{
	/// <summary>
	/// The document a <c>chat.turn.begin</c> hook receives: the turn is about to start and nothing has run yet.
	/// </summary>
	/// <remarks>
	/// Deliberately thin: this point is a circuit breaker for the whole turn, so it carries the tail of the history
	/// and not the round. A hook that needs to inspect what was asked belongs on <c>chat.turn.agent.begin</c>,
	/// which is asked the same turn one step later and carries the whole round.
	/// </remarks>
	public sealed class TurnBeginHookPayload
	{
		/// <summary>
		/// The chat the turn belongs to, or <see langword="null"/> when the world has none.
		/// </summary>
		public int? ChatId { get; init; }

		/// <summary>
		/// The last message of the history, or <see langword="null"/> when the chat is empty.
		/// </summary>
		public ChatHookMessageInfo? LastMessage { get; init; }
	}
}
