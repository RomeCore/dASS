using LLMDesktopAssistant.StructuredValues;

namespace LLMDesktopAssistant.Hooks
{
	public interface IHookDispatcher
	{
		Task<INodeValue?> ExecuteHooksAsync(INodeValue? payload,
			string hookType, string? hookName, CancellationToken cancellationToken = default);
	}
}
