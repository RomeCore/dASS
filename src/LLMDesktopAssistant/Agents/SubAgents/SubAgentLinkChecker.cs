
using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Prompting.Skills;
using LLMDesktopAssistant.Tools;

namespace LLMDesktopAssistant.Agents.SubAgents;

/// <summary>
/// The kind of a broken sub-agent dependency.
/// </summary>
public enum SubAgentLinkIssueKind
{
	/// <summary>
	/// A referenced tool does not exist.
	/// </summary>
	Tool,

	/// <summary>
	/// A referenced skill does not exist.
	/// </summary>
	Skill,

	/// <summary>
	/// A referenced sub-agent does not exist.
	/// </summary>
	SubAgent,

	/// <summary>
	/// A referenced memory block does not exist.
	/// </summary>
	MemoryBlock
}

/// <summary>
/// Describes a broken dependency of a sub-agent: a referenced skill, sub-agent or memory block
/// that is not available in the current session.
/// </summary>
public sealed record SubAgentLinkIssue(SubAgentLinkIssueKind Kind, string Name);

/// <summary>
/// Validates the dependencies of a <see cref="SubAgentInfo"/> against the sets of available
/// skills, sub-agents and memory blocks, producing a list of broken references.
/// </summary>
public static class SubAgentLinkChecker
{
	/// <summary>
	/// Checks the dependencies of the specified sub-agent against the available sets.
	/// </summary>
	/// <param name="info">The sub-agent to check.</param>
	/// <param name="skillNames">The names of all available skills.</param>
	/// <param name="subAgentNames">The names of all available sub-agents.</param>
	/// <param name="memoryBlockNames">The names of all available memory blocks.</param>
	/// <returns>The list of broken references.</returns>
	public static ImmutableList<SubAgentLinkIssue> Check(SubAgentInfo info, AddonSet<ToolInfo> tools,
		AddonSet<SkillInfo> skills, AddonSet<SubAgentInfo> subAgents, HashSet<string> memoryBlockNames)
	{
		var issues = ImmutableList.CreateBuilder<SubAgentLinkIssue>();

		var subAgentTools = info.AllowedTools.Concat(info.AvailableTools).Concat(info.DisallowedTools).Select(t => t.ToolName);
		foreach (var tool in subAgentTools)
		{
			if (!tools.ContainsName(tool))
				issues.Add(new SubAgentLinkIssue(SubAgentLinkIssueKind.Tool, tool));
		}

		foreach (var skill in info.Skills)
		{
			if (!skills.ContainsName(skill))
				issues.Add(new SubAgentLinkIssue(SubAgentLinkIssueKind.Skill, skill));
		}

		foreach (var subAgent in info.SubAgents)
		{
			if (!subAgents.ContainsName(subAgent))
				issues.Add(new SubAgentLinkIssue(SubAgentLinkIssueKind.SubAgent, subAgent));
		}

		foreach (var blockName in info.MemoryBlocks.Keys)
		{
			if (!memoryBlockNames.Contains(blockName))
				issues.Add(new SubAgentLinkIssue(SubAgentLinkIssueKind.MemoryBlock, blockName));
		}

		return issues.ToImmutable();
	}
}
