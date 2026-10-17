using LLMDesktopAssistant.Tools.Execution.Domain;

namespace LLMDesktopAssistant.Tools.Execution.Contexts
{
	/// <summary>
	/// Creates the execution context for a descriptor's home scope. Carried by the descriptor; the
	/// provider is the most-scoped one available (chat scope, the task's triggered chat, or the root).
	/// </summary>
	/// <remarks>
	/// The factory isolates the difference between the two worlds: it knows which concrete context to
	/// build and which service provider to expose, while the orchestrator only passes the invocation
	/// inputs.
	/// </remarks>
	public interface IToolExecutionContextFactory
	{
		/// <summary>
		/// Builds the context for one invocation.
		/// </summary>
		/// <param name="parameters">The source-supplied invocation inputs.</param>
		/// <returns>The concrete context the stages and the tool work with.</returns>
		/// <exception cref="InvalidOperationException">
		/// The factory's world requires a host (for example, a chat invocation without a chat message).
		/// </exception>
		IToolExecutionContext CreateContext(in ToolContextCreationParams parameters);
	}
}
