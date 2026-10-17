namespace LLMDesktopAssistant.Tools;

/// <summary>
/// Defines which policy/approval decisions a tool handles itself instead of the standard
/// HITL (Human-In-The-Loop) pipeline.
/// When set on <see cref="LLMDesktopAssistant.Tools.Execution.Domain.IToolDescriptor.HandledDecisions"/>,
/// the tool overrides the standard pipeline and handles the decisions itself.
/// </summary>
[Flags]
public enum ToolPolicyDecision
{
	/// <summary>
	/// The tool does not override the standard HITL pipeline.
	/// The <see cref="LLM.Services.Tools.IChatToolExecutionService"/> will use
	/// the normal approval flow based on <see cref="ToolApprovalLevel"/>
	/// and agent policy settings.
	/// </summary>
	None = 0,

	/// <summary>
	/// The tool approves the operation on preview stage and will handle
	/// execution without requiring user interaction. The standard approval
	/// step is skipped, and the tool is executed immediately.
	/// Useful for operations that are safe or have been pre-validated
	/// (e.g., auto-applying a diff after computing it in preview).
	/// </summary>
	Approve = 1 << 0,

	/// <summary>
	/// The tool requests user confirmation via its own UI flow
	/// (e.g., <see cref="Forms.FormsToolModule"/> or a custom diff preview).
	/// The <see cref="LLM.Services.Tools.IChatToolExecutionService"/> must wait
	/// for the tool's own confirmation mechanism before proceeding.
	/// When <see cref="LLMDesktopAssistant.Tools.Execution.Domain.IToolExecutionContext.RunningInUi"/> is <see langword="false"/>,
	/// this should be treated as <see cref="Approve"/>.
	/// </summary>
	Ask = 1 << 1,

	/// <summary>
	/// The tool denies the operation on preview stage. Execution is
	/// aborted immediately with an error message provided by the tool.
	/// Useful for rejecting dangerous operations (e.g., deleting system files).
	/// </summary>
	Disallow = 1 << 2,
}
