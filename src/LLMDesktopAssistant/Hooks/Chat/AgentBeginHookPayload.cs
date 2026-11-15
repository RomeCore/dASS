namespace LLMDesktopAssistant.Hooks.Chat
{
	/// <summary>
	/// The document a <c>chat.turn.agent.begin</c> hook receives: the orderer has to choose the next agent, and the
	/// hook may choose instead.
	/// </summary>
	/// <remarks>
	/// The round is the trailing run of consecutive user and assistant messages - the same window the execution
	/// stages are chosen against - so the document is bounded by the round and not by the whole history. Every
	/// message carries its model-facing text: a hook that decides by content must see what the agent would see.
	/// <para/>
	/// This event does not fire when a forced agent is in play (the orderer returns it before asking anybody) nor
	/// when an agent continues its own tool batch (<c>chat.turn.agent.continue</c> reports that).
	/// </remarks>
	public sealed class AgentBeginHookPayload
	{
		/// <summary>
		/// The chat the turn belongs to, or <see langword="null"/> when the world has none.
		/// </summary>
		public int? ChatId { get; init; }

		/// <summary>
		/// The messages of the current round, in history order.
		/// </summary>
		public ChatHookMessageInfo[] Messages { get; init; } = [];

		/// <summary>
		/// The agents that already ran in this round, in the order they ran.
		/// </summary>
		public ChatHookAgentInfo[] ExecutedInThisRound { get; init; } = [];

		/// <summary>
		/// The agent that ran last, or <see langword="null"/> when the round is just starting.
		/// </summary>
		public ChatHookAgentInfo? PreviousAgent { get; init; }

		/// <summary>
		/// The agents the orderer could pick from: every agent of every enabled stage, with its stage.
		/// </summary>
		public ChatHookAgentInfo[] Available { get; init; } = [];
	}
}
