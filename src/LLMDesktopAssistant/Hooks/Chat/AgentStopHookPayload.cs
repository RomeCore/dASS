namespace LLMDesktopAssistant.Hooks.Chat
{
	/// <summary>
	/// The document a <c>chat.turn.agent.stop.*.*</c> hook receives: one agent's turn is over.
	/// </summary>
	/// <remarks>
	/// Status and reason are repeated from the event name because a hook that matches the whole family needs them in
	/// the document - the same rule the tool result payload follows. A run that produced no message at all (the
	/// agent could not be resolved, the very first request failed) reports <c>error</c>, or <c>cancelled</c> when the
	/// turn was cancelled.
	/// </remarks>
	public sealed class AgentStopHookPayload
	{
		/// <summary>
		/// The chat the turn belongs to, or <see langword="null"/> when the world has none.
		/// </summary>
		public int? ChatId { get; init; }

		/// <summary>
		/// The id of the agent whose turn ended.
		/// </summary>
		public Guid AgentId { get; init; }

		/// <summary>
		/// The name of the agent whose turn ended.
		/// </summary>
		public string Agent { get; init; } = string.Empty;

		/// <summary>
		/// The id of the execution stage the agent ran in.
		/// </summary>
		public Guid StageId { get; init; }

		/// <summary>
		/// The status of the agent's last message in kebab case: <c>success</c>, <c>error</c> or <c>cancelled</c>.
		/// </summary>
		public string Status { get; init; } = string.Empty;

		/// <summary>
		/// Why the agent's turn ended in kebab case: <c>complete</c> or <c>interrupted</c>.
		/// </summary>
		public string Reason { get; init; } = string.Empty;

		/// <summary>
		/// How many messages the agent produced in this run.
		/// </summary>
		public int Cycles { get; init; }

		/// <summary>
		/// The database ids of the messages the agent produced in this run, or <see langword="null"/> when none of
		/// them is saved.
		/// </summary>
		public int[]? MessageIds { get; init; }
	}
}
