using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Agents.SubAgents;
using LLMDesktopAssistant.Agents.Tasks;
using LLMDesktopAssistant.Tools;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Tools.Specifiers;

namespace LLMDesktopAssistant.LLM.Services.Agents
{
	/// <summary>
	/// The default implementation of the <see cref="ISubAgentToolResolver"/> interface.
	/// </summary>
	[ChatService(typeof(ISubAgentToolResolver))]
	public class SubAgentToolResolver(
		IAddonSetCollector<ToolInfo> toolsetCollector
	) : ISubAgentToolResolver
	{
		/// <inheritdoc/>
		public IEnumerable<IToolDescriptor> ResolveSubAgentTools(SubAgentInfo agentInfo, out List<string> errors)
		{
			errors = [];

			// Tools are looked up by name or alias so that both primary names and aliases (e.g. Claude Code
			// compatible names) can be used in sub-agent definitions. The lookup deliberately uses the available
			// set: a sub-agent has its own permissions and must not inherit the caller agent's tool restrictions.
			var toolset = toolsetCollector.GetAvailableAddons();
			var resolved = new Dictionary<string, IToolDescriptor>(StringComparer.Ordinal);

			// Available tools are added first with the lowest priority.
			foreach (var tool in agentInfo.AvailableTools)
			{
				if (toolset.GetByName(tool.ToolName) is { } toolInfo && !resolved.ContainsKey(toolInfo.Name))
					resolved[toolInfo.Name] = CreateAgentTool(toolInfo, ToolApprovalLevel.PolicyBased);
			}

			// Allowed tools override available tools.
			foreach (var tool in agentInfo.AllowedTools)
			{
				if (toolset.GetByName(tool.ToolName) is { } toolInfo)
					resolved[toolInfo.Name] = CreateAgentTool(toolInfo, ToolApprovalLevel.AlwaysApprove);
			}

			// Disallowed tools override everything.
			foreach (var tool in agentInfo.DisallowedTools)
			{
				if (toolset.GetByName(tool.ToolName) is { } toolInfo)
					resolved[toolInfo.Name] = CreateAgentTool(toolInfo, ToolApprovalLevel.AlwaysDisallow);
			}

			return resolved.Values;
		}

		private static IToolDescriptor CreateAgentTool(ToolInfo toolInfo, ToolApprovalLevel approvalLevel) =>
			TaskToolProjection.Project(toolInfo, approvalLevel);
	}
}
