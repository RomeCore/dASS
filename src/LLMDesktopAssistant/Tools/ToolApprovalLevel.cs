using LLMDesktopAssistant.Agents;

namespace LLMDesktopAssistant.Tools;

/// <summary>
/// Defines the approval policy for a tool - whether execution requires
/// user interaction, is automatically allowed, or follows agent-level
/// behaviour policies (<see cref="AgentToolSettings.AutoApproveBehaviours"/>
/// and <see cref="AgentToolSettings.DisallowBehaviours"/>).
/// </summary>
public enum ToolApprovalLevel
{
	/// <summary>
	/// Defer the decision to the agent's behaviour-based policy settings.
	/// The execution service will check <see cref="AgentToolSettings.AutoApproveBehaviours"/> and
	/// <see cref="AgentToolSettings.DisallowBehaviours"/> on the sending agent to determine whether
	/// to auto-approve, prompt, or reject the tool call.
	/// This is the recommended default for most tools.
	/// </summary>
	PolicyBased = 0,

	/// <summary>
	/// Defer to the agent's policy settings, but prompt the user even if
	/// the agent has auto-approved this behaviour (see <see cref="AgentToolSettings.AutoApproveBehaviours"/>).
	/// </summary>
	PolicyAskOrDisallow = 1,

	/// <summary>
	/// Defer to the agent's policy settings, never disallow tool execution,
	/// so the <see cref="AgentToolSettings.DisallowBehaviours"/> setting is ignored.
	/// </summary>
	PolicyApproveOrAsk = 2,

	/// <summary>
	/// Defer to the agent's policy settings, but never prompt the user and auto-approve
	/// unless explicitly disallowed by the agent (see <see cref="AgentToolSettings.DisallowBehaviours"/>).
	/// So, the <see cref="AgentToolSettings.AutoApproveBehaviours"/> setting is ignored.
	/// Approve-first approach: execute unless explicitly disallowed.
	/// </summary>
	PolicyAutoApproveUnlessDisallowed = 3,

	/// <summary>
	/// Defer to the agent's policy settings, but never prompt the user and auto-disallow
	/// unless explicitly approved by the agent (see <see cref="AgentToolSettings.AutoApproveBehaviours"/>).
	/// So, the <see cref="AgentToolSettings.DisallowBehaviours"/> setting is ignored.
	/// Disallow-first approach: reject unless explicitly auto-approved.
	/// </summary>
	PolicyAutoDisallowUnlessApproved = 4,

	/// <summary>
	/// Execute the tool immediately without any user interaction.
	/// This bypasses all agent-level policy checks.
	/// Use for trivially safe operations (math, random, time).
	/// </summary>
	AlwaysApprove = 5,

	/// <summary>
	/// Always prompt the user for confirmation before executing the tool,
	/// regardless of the agent's <see cref="AgentToolSettings.AutoApproveBehaviours"/>.
	/// Use for high-impact operations like file deletion or code execution.
	/// </summary>
	AlwaysAsk = 6,

	/// <summary>
	/// Always reject the tool call without executing it, regardless of the agent's policy settings.
	/// This explicitly notifies agents that they are not allowed to execute this tool upon trying to do so.
	/// </summary>
	AlwaysDisallow = 7,
}
