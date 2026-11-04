namespace LLMDesktopAssistant.LLM.Domain
{
	/// <summary>
	/// The earliest barrier at which a pending follow-up may be injected into the chat.
	/// <para/>
	/// The values run from the earliest gate to the latest, so the minimum across the queue is the gate the whole
	/// queue waits for. This is deliberately not <c>ChatExecutionLevel</c>: there the values run widest-to-narrowest
	/// and taking the minimum would mean the opposite.
	/// </summary>
	public enum FollowUpInjectionLevel
	{
		/// <summary>
		/// No barrier: the running generation is cancelled at once and the queue is drained as soon as the chat is idle.
		/// </summary>
		Immediate = 0,

		/// <summary>
		/// Injected once the tool batch of the current message is closed.
		/// </summary>
		Message = 1,

		/// <summary>
		/// Injected once one agent's cycle is over (its last message carries no tool calls).
		/// </summary>
		Agent = 2,

		/// <summary>
		/// Injected once the whole multi-agent sequence is over.
		/// </summary>
		MultiAgentSequence = 3,
	}
}
