using LLMDesktopAssistant.Agents;
using LLMDesktopAssistant.Prompting.Context;

namespace LLMDesktopAssistant.LLM.Services.Prompting
{
	/// <summary>
	/// Captures and persists the supersede stamps for the pending assistant message.
	/// </summary>
	public interface IPromptSupersedeContextProcessor
	{
		/// <param name="agent">The agent to process.</param>
		/// <param name="effectiveContext">The effective chat context of the agent.</param>
		/// <param name="contexts">
		/// The prompt context addons of the agent. The processor picks the supersede ones and hands each provider
		/// its own addon, so a provider never guesses which context it belongs to.
		/// </param>
		/// <param name="cancellationToken">The cancellation token forwarded to the providers.</param>
		Task ProcessAsync(ChatAgentDescriptor agent, EffectiveChatContext effectiveContext,
			IEnumerable<PromptContextInfo> contexts, CancellationToken cancellationToken = default);
	}
}
