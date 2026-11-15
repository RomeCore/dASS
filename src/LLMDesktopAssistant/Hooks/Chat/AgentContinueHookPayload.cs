namespace LLMDesktopAssistant.Hooks.Chat
{
	/// <summary>
	/// The document a <c>chat.turn.agent.continue</c> hook receives: an agent runs again without a fresh selection.
	/// </summary>
	/// <remarks>
	/// The two reasons are different moments of the same pipeline. <c>pending-tool-calls</c> is a resume - the turn
	/// found a message with unanswered calls and continues its sender, so no selection happened and
	/// <c>chat.turn.agent.begin</c> did not fire. <c>tool-cycle</c> is the ordinary loop inside one agent: it
	/// produced tool calls, the batch ran, and it continues with the results.
	/// </remarks>
	public sealed class AgentContinueHookPayload
	{
		/// <summary>
		/// The chat the turn belongs to, or <see langword="null"/> when the world has none.
		/// </summary>
		public int? ChatId { get; init; }

		/// <summary>
		/// The id of the agent that continues.
		/// </summary>
		public Guid AgentId { get; init; }

		/// <summary>
		/// The name of the agent that continues.
		/// </summary>
		public string Agent { get; init; } = string.Empty;

		/// <summary>
		/// The id of the execution stage the agent runs in.
		/// </summary>
		public Guid StageId { get; init; }

		/// <summary>
		/// The zero-based cycle the agent is about to run, or <see langword="null"/> for a resume whose earlier
		/// cycles belong to a previous turn and cannot be counted without walking the history.
		/// </summary>
		public int? Cycle { get; init; }

		/// <summary>
		/// The reason in kebab case: <c>pending-tool-calls</c> or <c>tool-cycle</c>.
		/// </summary>
		public string Reason { get; init; } = string.Empty;

		/// <summary>
		/// The names of the tool calls the agent has to finish, or <see langword="null"/> for a cycle continuation.
		/// </summary>
		public string[]? PendingToolCalls { get; init; }

		/// <summary>
		/// The database id of the message being continued, or <see langword="null"/> when it is not saved.
		/// </summary>
		public int? LastMessageId { get; init; }
	}
}
