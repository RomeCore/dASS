namespace LLMDesktopAssistant.LLM.Services
{
	/// <summary>
	/// The scope of a chat execution token. Levels nest from the widest (<see cref="Operation"/>) to the
	/// narrowest (<see cref="Message"/>): cancelling a wider level cancels every narrower one, never the other way around.
	/// Levels may be skipped (a <see cref="Message"/> token without an active <see cref="Operation"/> is valid).
	/// </summary>
	public enum ChatExecutionLevel
	{
		/// <summary>
		/// No level. Cannot be taken.
		/// </summary>
		None = 0,

		/// <summary>
		/// The widest level: a chat mutation (send, edit, regenerate, switch branch, ...). Cancelling it cancels
		/// everything running in the chat — generation, agent routing, commands.
		/// </summary>
		Operation = 1,

		/// <summary>
		/// A single slash-command execution.
		/// </summary>
		Command = 2,

		/// <summary>
		/// A whole agent sequence within one chat execution.
		/// </summary>
		AgentSequence = 3,

		/// <summary>
		/// A single agent's execution.
		/// </summary>
		Agent = 4,

		/// <summary>
		/// The narrowest level: one message generation.
		/// </summary>
		Message = 5
	}
}
