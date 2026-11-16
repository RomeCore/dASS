namespace LLMDesktopAssistant.Agents.Tasks
{
	public enum AgentTaskLaunchSource
	{
		/// <summary>
		/// Unknown source of the task.
		/// </summary>
		Unknown,

		/// <summary>
		/// The task was launched by an internal process or system, such as chat naming.
		/// </summary>
		Internal,

		/// <summary>
		/// The task was launched by a user action, such as clicking a button or entering a command.
		/// </summary>
		Manual,

		/// <summary>
		/// The task was launched by a script (usually Lua).
		/// </summary>
		Script,

		/// <summary>
		/// The task was launched by a tool (agent-call or agent-callsub).
		/// </summary>
		Tool
	}
}
