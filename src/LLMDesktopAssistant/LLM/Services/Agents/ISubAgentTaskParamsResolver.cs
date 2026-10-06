using LLMDesktopAssistant.Agents.Tasks;
using LLMDesktopAssistant.Tools;

namespace LLMDesktopAssistant.LLM.Services.Agents
{
	public interface ISubAgentTaskParamsResolver
	{
		/// <summary>
		/// Resolves the launch parameters for a sub-agent call.
		/// </summary>
		/// <param name="sourceParameters">The launch parameters of the calling task.</param>
		/// <param name="descriptor">The sub-agent descriptor to resolve.</param>
		/// <param name="additionalMessages">The messages to append after the sub-agent system prompt.</param>
		/// <param name="errors">The list of resolution errors.</param>
		/// <param name="policyOverride">
		/// The tool behaviour policy to apply instead of the source policy (the chat-level sub-agent policy).
		/// When <see langword="null"/>, the source policy is kept.
		/// </param>
		AgentTaskLaunchParameters Resolve(AgentTaskLaunchParameters sourceParameters, TaskSubAgentDescriptor descriptor,
			IEnumerable<AgentChatMessage> additionalMessages, out List<string> errors, ToolPolicyMask? policyOverride = null);
	}
}
