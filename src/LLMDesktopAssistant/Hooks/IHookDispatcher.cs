using LLMDesktopAssistant.StructuredValues;

namespace LLMDesktopAssistant.Hooks
{
	public interface IHookDispatcher
	{
		Task<INodeValue?> ExecuteHooksAsync(INodeValue? payload,
			string hookType, string? hookName, HookExecutionFlow defaultFlow,
			HookExecutionFlow? forcedFlow = null, CancellationToken cancellationToken = default);
	}
}
