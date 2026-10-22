using LLMDesktopAssistant.Agents;

namespace LLMDesktopAssistant.Prompting.Context
{
	public interface IPromptSectionStateProvider<TState>
		where TState : PromptSectionStateBase
	{
		Task<TState?> CaptureStateAsync(ChatAgentDescriptor agent, CancellationToken cancellationToken = default);
	}
}
