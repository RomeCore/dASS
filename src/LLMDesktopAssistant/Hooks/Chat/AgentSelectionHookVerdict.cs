namespace LLMDesktopAssistant.Hooks.Chat
{
	/// <summary>
	/// What a <c>chat.turn.agent.begin</c> hook answers: whether the turn should pick an agent at all, and which one.
	/// </summary>
	/// <remarks>
	/// A hook may name an agent instead of letting the stages decide - by id or by name - and the stage is resolved
	/// the same way a forced agent's is: the first enabled stage containing it, then the first stage containing it,
	/// then <see cref="Guid.Empty"/>.
	/// <para/>
	/// <see cref="ChatHookDecision.Disallow"/> and <see cref="ChatHookDecision.Error"/> both end the turn as if no
	/// agent had been selected, and the difference is only what the log says. An agent name that does not resolve is
	/// a warning and "no opinion", not a refusal: a typo in a selector must not silence the chat.
	/// </remarks>
	public sealed class AgentSelectionHookVerdict
	{
		/// <summary>
		/// The hook's opinion, or <see langword="null"/> for "no opinion".
		/// </summary>
		public ChatHookDecision? Decision { get; init; }

		/// <summary>
		/// The agent to run next, by id or by name, or <see langword="null"/> to let the stages choose.
		/// </summary>
		public string? Agent { get; init; }

		/// <summary>
		/// Why the hook refused the turn. It reaches the log only: there is no surface to show it on while a
		/// generation is not running. Ignored when the decision is not a refusal.
		/// </summary>
		public string? Reason { get; init; }
	}
}
