using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.Tools.Execution.Domain;

namespace LLMDesktopAssistant.Tools.Execution.Contexts
{
	/// <summary>
	/// Creates contexts for tools running inside an agent task. The service provider is the triggered
	/// chat's scope when the task was launched from a chat, otherwise the root.
	/// </summary>
	/// <remarks>
	/// The task world has no persisted consent memorization of its own: the task's toolset is updated
	/// in memory by the task path, so the context carries no consent context here.
	/// </remarks>
	public sealed class TaskToolContextFactory(Chat? chat, IServiceProvider services) : IToolExecutionContextFactory
	{
		/// <summary>
		/// Builds the task context for one invocation.
		/// </summary>
		/// <param name="parameters">The source-supplied invocation inputs.</param>
		/// <returns>The task context with the task's provider and triggered chat.</returns>
		public IToolExecutionContext CreateContext(in ToolContextCreationParams parameters) =>
			new AgentTaskExecutionContext(parameters, chat, services);
	}
}
