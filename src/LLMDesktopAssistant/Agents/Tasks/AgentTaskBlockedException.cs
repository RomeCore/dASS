namespace LLMDesktopAssistant.Agents.Tasks
{
	/// <summary>
	/// What a caller that awaited a task gets when the launch was blocked before it ran.
	/// </summary>
	/// <remarks>
	/// The exception exists for the awaiter: it is what a waiting caller receives before it sees any message. The task
	/// keeps it in <see cref="AgentTask.Exception"/>, so a caller that did not wait - <c>agent-call</c> with
	/// <c>wait: false</c> - finds the same text there or in <see cref="AgentTask.BlockReason"/>.
	/// </remarks>
	/// <param name="reason">Why the launch was blocked.</param>
	public sealed class AgentTaskBlockedException(string reason) : Exception(reason);
}
