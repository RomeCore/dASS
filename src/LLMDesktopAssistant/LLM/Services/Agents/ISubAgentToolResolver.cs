using LLMDesktopAssistant.Agents.SubAgents;
using LLMDesktopAssistant.Agents.Tasks;
using LLMDesktopAssistant.Tools.Execution.Domain;

namespace LLMDesktopAssistant.LLM.Services.Agents
{
	/// <summary>
	/// Resolves the tools available to a sub-agent.
	/// </summary>
	public interface ISubAgentToolResolver
	{
		/// <summary>
		/// Resolves the tools of the specified sub-agent to <see cref="IToolDescriptor"/> instances.
		/// </summary>
		/// <param name="agentInfo">The sub-agent information to resolve tools for.</param>
		/// <param name="errors">A list of errors encountered during the resolution process.</param>
		/// <returns>A list of <see cref="IToolDescriptor"/> instances available to the sub-agent.</returns>
		IEnumerable<IToolDescriptor> ResolveSubAgentTools(SubAgentInfo agentInfo, out List<string> errors);
	}
}
