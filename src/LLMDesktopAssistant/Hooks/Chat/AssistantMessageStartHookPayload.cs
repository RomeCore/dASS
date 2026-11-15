namespace LLMDesktopAssistant.Hooks.Chat
{
	/// <summary>
	/// The document a <c>message.assistant.start</c> hook receives.
	/// </summary>
	/// <remarks>
	/// Carries identity only: the message has just been created with status <c>Pending</c> and has no content yet.
	/// Anything else a hook needs - the history, the agent's settings, the toolset - it resolves from the chat.
	/// </remarks>
	public sealed class AssistantMessageStartHookPayload
	{
		/// <summary>
		/// The chat the message belongs to, or <see langword="null"/> when the world has none.
		/// </summary>
		public int? ChatId { get; init; }

		/// <summary>
		/// The database id of the pending message.
		/// </summary>
		public int? MessageId { get; init; }

		/// <summary>
		/// The 1-based index of the message in the history.
		/// </summary>
		public int? Index { get; init; }

		/// <summary>
		/// The id of the agent that is about to answer.
		/// </summary>
		public Guid AgentId { get; init; }

		/// <summary>
		/// The name of the agent that is about to answer.
		/// </summary>
		public string Agent { get; init; } = string.Empty;

		/// <summary>
		/// The id of the execution stage the agent runs in.
		/// </summary>
		public Guid StageId { get; init; }

		/// <summary>
		/// The zero-based cycle of this agent: 0 for its first message, 1 after its first tool batch, and so on.
		/// </summary>
		public int Cycle { get; init; }

		/// <summary>
		/// The model that will answer.
		/// </summary>
		public string Model { get; init; } = string.Empty;
	}
}
