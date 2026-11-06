using LLMDesktopAssistant.StructuredValues;

namespace LLMDesktopAssistant.Hooks
{
	public interface IHookExecutor
	{
		/// <summary>
		/// Executes the hook asynchronously. The payload is the data that the hook is being applied to,
		/// and the previous value is the result of the previous hook in the chain.
		/// </summary>
		/// <param name="services">The chat-scoped service provider.</param>
		/// <param name="payload">The input data for the hook that received from the caller.</param>
		/// <param name="previous">The result of the previous hook in the chain, if any. If this is the first hook, this will be null.</param>
		/// <param name="hookType">The type of hook that is being executed.</param>
		/// <param name="hookName">The name (or key) of the hook that is being executed. This is usually a tool name or command name.</param>
		/// <param name="cancellationToken">The cancellation token to use for the operation.</param>
		/// <returns>The result of the hook.</returns>
		Task<HookExecutionResult> ExecuteAsync(IServiceProvider services, INodeValue? payload, INodeValue? previous,
			string hookType, string? hookName, CancellationToken cancellationToken = default);
	}
}
