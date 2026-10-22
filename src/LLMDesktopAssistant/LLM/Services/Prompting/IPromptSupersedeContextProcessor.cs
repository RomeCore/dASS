using LLMDesktopAssistant.Agents;
using LLMDesktopAssistant.Prompting.Context;

namespace LLMDesktopAssistant.LLM.Services.Prompting
{
	/// <summary>
	/// Captures and persists the supersede stamps for the pending assistant message.
	/// </summary>
	public interface IPromptSupersedeContextProcessor
	{
		Task ProcessAsync(ChatAgentDescriptor agent, EffectiveChatContext effectiveContext,
			IEnumerable<IPromptSupersedeContextProvider> providers, CancellationToken cancellationToken = default);
	}
}
