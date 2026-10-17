using LLMDesktopAssistant.Tools.Execution.Contexts;
using LLMDesktopAssistant.Tools.Execution.Domain;

namespace LLMDesktopAssistant.Tools.Execution.Dispatch
{
	/// <summary>
	/// The front controller of tool invocations. It builds the invocation context from the request and
	/// hands it to the orchestrator the source selects.
	/// </summary>
	/// <remarks>
	/// The dispatcher is stateless and knows nothing about chats, tasks or scripts: the source-specific
	/// values (context factory, confirmation sink, effective behaviours, host, argument stream) arrive in
	/// the request. Every entry point — the chat execution service, a nested script call, the task
	/// executor — is therefore the same call.
	/// </remarks>
	public interface IToolInvocationDispatcher
	{
		/// <summary>
		/// Runs one invocation.
		/// </summary>
		/// <param name="request">The invocation inputs, assembled by the entry point.</param>
		/// <param name="ct">Cancellation of the run.</param>
		/// <returns>A task that completes when the call has settled.</returns>
		Task RunAsync(ToolInvocationRequest request, CancellationToken ct);
	}
}
