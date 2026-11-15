namespace LLMDesktopAssistant.Hooks.Chat
{
	/// <summary>
	/// One agent together with the execution stage it ran or is configured in, as the turn hooks carry it.
	/// </summary>
	/// <remarks>
	/// Shared by the payloads instead of nested in each of them, because <c>chat.turn.agent.begin</c> and
	/// <c>chat.turn.stop</c> describe the same pair - once as a choice, once as a record of what ran - and a hook
	/// that handles both should read one shape.
	/// </remarks>
	public sealed class ChatHookAgentInfo
	{
		/// <summary>
		/// The id of the agent.
		/// </summary>
		public Guid AgentId { get; init; }

		/// <summary>
		/// The name of the agent, or an empty string when the descriptor no longer resolves.
		/// </summary>
		public string Agent { get; init; } = string.Empty;

		/// <summary>
		/// The id of the execution stage the agent belongs to, or <see cref="Guid.Empty"/> for an agent that no
		/// stage contains.
		/// </summary>
		public Guid StageId { get; init; }
	}
}
