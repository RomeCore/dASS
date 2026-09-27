using LLMDesktopAssistant.Agents;
using LLMDesktopAssistant.LLM.Domain;

namespace LLMDesktopAssistant.LLM.Services.Prompting
{
	/// <summary>
	/// Determines whether chat messages are visible to a given agent, respecting the agent's
	/// read permissions, the sender agent's exposure mode and the message visibility settings.
	/// </summary>
	public interface IMessageVisibilityService
	{
		/// <summary>
		/// Determines whether the specified message is visible to the given agent.
		/// </summary>
		/// <param name="message">The branched message to check.</param>
		/// <param name="agent">The agent to check visibility for.</param>
		/// <returns>The visibility result.</returns>
		MessageVisibilityResult CheckVisibility(BranchedMessage message, ChatAgentDescriptor agent);
	}
}
