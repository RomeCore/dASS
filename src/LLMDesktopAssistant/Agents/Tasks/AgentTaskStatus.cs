namespace LLMDesktopAssistant.Agents.Tasks
{
	public enum AgentTaskStatus
	{
		Pending,

		Executing,

		Success,

		Failed,

		Cancelled,

		/// <summary>
		/// The launch was blocked before it ran a single request; <see cref="AgentTask.BlockReason"/> says why.
		/// </summary>
		Blocked
	}
}
