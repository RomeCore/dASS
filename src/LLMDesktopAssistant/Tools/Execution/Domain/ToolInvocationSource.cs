namespace LLMDesktopAssistant.Tools.Execution.Domain
{
	/// <summary>
	/// The entry point a tool invocation came from. The source selects the orchestrator and, with it,
	/// how much of the approval pipeline runs.
	/// </summary>
	public enum ToolInvocationSource
	{
		/// <summary>
		/// An agent chose the tool during a chat run. The full pipeline runs: policy, specifiers,
		/// confirmation and execution.
		/// </summary>
		ChatRun,

		/// <summary>
		/// The user re-ran an existing tool call from the chat. This is the user's own action, so policy
		/// and confirmation are skipped, but the hard floor is still honoured.
		/// </summary>
		ChatRestart,

		/// <summary>
		/// The user ran the tool through its derived slash command. Like a restart, this is the user's
		/// own action: policy and confirmation are skipped, the hard floor is honoured.
		/// </summary>
		Command,

		/// <summary>
		/// An agent chose the tool inside an agent task. The full pipeline runs, with the task as the
		/// confirmation host.
		/// </summary>
		AgentTask,

		/// <summary>
		/// A script (Lua, C# script, Python) called the tool. Scripts bypass policy, confirmation and the
		/// hard floor entirely; only validation, synchronization, preview, execution and settlement run.
		/// </summary>
		Script
	}
}
