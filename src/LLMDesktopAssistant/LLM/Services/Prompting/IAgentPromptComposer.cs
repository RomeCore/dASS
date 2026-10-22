using LLMDesktopAssistant.Agents;

namespace LLMDesktopAssistant.LLM.Services.Prompting
{
	/// <summary>
	/// The central prompt composer: builds the atomic pair (messages + tools) for an agent request.
	/// Owns the toolset cache invalidation and the system prompt header selection.
	/// </summary>
	public interface IAgentPromptComposer
	{
		/// <summary>
		/// Builds the prompt bundle for the given agent.
		/// The build is asynchronous because prompt context providers may execute scripts.
		/// </summary>
		/// <param name="agent">The agent to build the prompt for.</param>
		/// <param name="cancellationToken">The cancellation token forwarded to prompt context providers.</param>
		Task<AgentPromptBundle> BuildAsync(ChatAgentDescriptor agent, CancellationToken cancellationToken = default);
	}
}
