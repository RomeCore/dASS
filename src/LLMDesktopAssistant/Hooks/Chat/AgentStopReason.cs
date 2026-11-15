namespace LLMDesktopAssistant.Hooks.Chat
{
	/// <summary>
	/// Why an agent's turn ended, carried as the last variant of <c>chat.turn.agent.stop.*.*</c>.
	/// </summary>
	public enum AgentStopReason
	{
		/// <summary>
		/// The agent produced a message with no tool calls and nothing cut it short.
		/// </summary>
		Complete,

		/// <summary>
		/// A follow-up message the user wrote while the agent ran took precedence: the agent's turn ends here
		/// instead of moving on to the next agent.
		/// </summary>
		Interrupted
	}
}
