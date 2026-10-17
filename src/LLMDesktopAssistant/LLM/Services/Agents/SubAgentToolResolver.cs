using LLMDesktopAssistant.Agents.SubAgents;
using LLMDesktopAssistant.Agents.Tasks;
using LLMDesktopAssistant.LLM.Services.Tools;
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
		IToolsetCacheService toolsetCache
	) : ISubAgentToolResolver
	{
		/// <inheritdoc/>
		public IEnumerable<IToolDescriptor> ResolveSubAgentTools(SubAgentInfo agentInfo, out List<string> errors)
		{
			errors = [];

			// Tools are looked up in the aliased tool dictionary so that both primary names
			// and aliases (e.g. Claude Code compatible names) can be used in sub-agent definitions.
			var toolLookup = toolsetCache.AliasedTools;
			var resolved = new Dictionary<string, IToolDescriptor>(StringComparer.Ordinal);

			// Available tools are added first with the lowest priority.
			foreach (var tool in agentInfo.AvailableTools)
			{
				if (toolLookup.TryGetValue(tool.ToolName, out var toolInfo) && !resolved.ContainsKey(toolInfo.Name))
					resolved[toolInfo.Name] = CreateAgentTool(toolInfo, ToolApprovalLevel.PolicyBased);
			}

			// Allowed tools override available tools.
			foreach (var tool in agentInfo.AllowedTools)
			{
				if (toolLookup.TryGetValue(tool.ToolName, out var toolInfo))
					resolved[toolInfo.Name] = CreateAgentTool(toolInfo, ToolApprovalLevel.AlwaysApprove);
			}

			// Disallowed tools override everything.
			foreach (var tool in agentInfo.DisallowedTools)
			{
				if (toolLookup.TryGetValue(tool.ToolName, out var toolInfo))
					resolved[toolInfo.Name] = CreateAgentTool(toolInfo, ToolApprovalLevel.AlwaysDisallow);
			}

			return resolved.Values;
		}

		private static IToolDescriptor CreateAgentTool(ToolInfo toolInfo, ToolApprovalLevel approvalLevel) =>
			TaskToolProjection.Project(toolInfo, approvalLevel);
	}
}
