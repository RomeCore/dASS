namespace LLMDesktopAssistant.Hooks.Chat
{
	/// <summary>
	/// Why an agent runs again without a fresh selection, carried as the reason of
	/// <c>chat.turn.agent.continue</c>.
	/// </summary>
	public enum AgentContinueReason
	{
		/// <summary>
		/// The turn resumed an agent whose previous message still has unanswered tool calls: the work was cut
		/// short by a restart or a new turn, and the agent has to finish it.
		/// </summary>
		PendingToolCalls,

		/// <summary>
		/// The agent finished a tool batch inside its own run and continues with the next cycle.
		/// </summary>
		ToolCycle
	}
}
