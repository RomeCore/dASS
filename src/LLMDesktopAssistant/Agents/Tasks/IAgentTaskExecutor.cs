namespace LLMDesktopAssistant.Agents.Tasks
{
	/// <summary>
	/// Represents an agent task executor that executes tasks asynchronously.
	/// </summary>
	public interface IAgentTaskExecutor
	{
		/// <summary>
		/// Gets the current executing task in this async context.
		/// </summary>
		AgentTask? Current { get; }

		/// <summary>
		/// Launches a new task with the given parameters.
		/// </summary>
		/// <param name="parameters">The parameters for the task.</param>
		/// <param name="cancellationToken">A token to cancel the task execution.</param>
		/// <returns>The task that can be tracked and awaited.</returns>
		AgentTask Execute(AgentTaskLaunchParameters parameters, CancellationToken cancellationToken = default);

		/// <summary>
		/// Continues the given task.
		/// </summary>
		/// <param name="task">The task to continue.</param>
		/// <param name="messages">The messages to append to the task.</param>
		/// <param name="cancellationToken">A token to cancel the task execution.</param>
		void Continue(AgentTask task, IEnumerable<AgentChatMessage> messages, CancellationToken cancellationToken = default);
	}
}
