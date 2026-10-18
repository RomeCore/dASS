using System.Collections.Immutable;
using System.Text.Json.Nodes;
using LLMDesktopAssistant.Tools.Execution.Contexts;
using LLMDesktopAssistant.Tools.Specifiers;

namespace LLMDesktopAssistant.Tools.Execution.Domain
{
	/// <summary>
	/// The description of a tool: identity, declared policy metadata and the capabilities it carries.
	/// Both the chat tool world and the agent-task tool world implement it.
	/// </summary>
	/// <remarks>
	/// The descriptor <b>carries</b> its executors instead of implementing them, so a chat tool can run
	/// in an agent task by handing over its descriptor unchanged. Only <see cref="Main"/> is mandatory;
	/// every other capability is optional and its absence is meaningful.
	/// </remarks>
	public interface IToolDescriptor
	{
		/// <summary>
		/// The unique tool name. It is the identity used by the model, the settings and memorization.
		/// </summary>
		string Name { get; }

		/// <summary>
		/// The description shown to the model.
		/// </summary>
		string Description { get; }

		/// <summary>
		/// The JSON schema of the tool arguments. Every invocation argument set is validated against it.
		/// </summary>
		JsonObject ArgumentSchema { get; }

		/// <summary>
		/// The behaviour used when preview does not override it. The policy stage reads it.
		/// </summary>
		ToolBehaviour DefaultExpectedBehaviour { get; }

		/// <summary>
		/// How the approval decision is made — policy-based, always approve, always ask, or always
		/// disallow.
		/// </summary>
		ToolApprovalLevel ApprovalLevel { get; }

		/// <summary>
		/// A per-tool mask that overrides the agent's policy for this tool, or <see langword="null"/>.
		/// Always-disallowed behaviours win over auto-approved ones.
		/// </summary>
		ToolPolicyMask? PolicyMask { get; }

		/// <summary>
		/// The specifier rules declared by the tool. Specifiers are evaluated only for policy-based
		/// approval levels.
		/// </summary>
		ImmutableList<ToolSpecifierRule> Specifiers { get; }

		/// <summary>
		/// How a specifier verdict combines with the policy decision, or <see langword="null"/> for the
		/// default soft combination.
		/// </summary>
		SpecifierBehaviourUnionMode? SpecifierUnionMode { get; }

		/// <summary>
		/// How multiple specifier rules aggregate into one verdict.
		/// </summary>
		SpecifierAggregationMode SpecifierAggregationMode { get; }

		/// <summary>
		/// The argument names a specifier is matched against.
		/// </summary>
		ImmutableList<string> SpecifierParameters { get; }

		/// <summary>
		/// Tool-wide synchronization keys, claimed by every invocation of the tool.
		/// </summary>
		ImmutableList<ToolSyncKey> StaticSyncKeys { get; }

		/// <summary>
		/// Which policy decisions the tool handles itself instead of the confirmation service. A tool may
		/// handle only <c>Ask</c> and leave <c>Disallow</c> to the service, or declare
		/// <see cref="ToolPolicyDecision.All"/>. This is the default for an invocation: preview may
		/// override it through <see cref="IToolExecutionContext.HandledDecisions"/>.
		/// </summary>
		ToolPolicyDecision HandledDecisions { get; }

		/// <summary>
		/// The approval-helper configuration, or <see langword="null"/> when the helper is not configured.
		/// </summary>
		ApprovalHelperConfig? ApprovalHelper { get; }

		/// <summary>
		/// The main executor. Required. It runs the argument set the pipeline validated.
		/// </summary>
		IToolMainExecutor Main { get; }

		/// <summary>
		/// The preview executor, or <see langword="null"/> when the tool has no preview.
		/// </summary>
		IToolPreviewExecutor? Preview { get; }

		/// <summary>
		/// The streaming analyzer, or <see langword="null"/> when partial arguments are not analysed.
		/// </summary>
		IToolStreamingAnalyzer? Streaming { get; }

		/// <summary>
		/// The specifier analyzer, or <see langword="null"/> when specifiers cannot be matched.
		/// </summary>
		IToolSpecifierAnalyzer? Specifier { get; }

		/// <summary>
		/// The synchronization analyzer that derives per-invocation keys from the arguments, or
		/// <see langword="null"/> when only static keys apply.
		/// </summary>
		IToolSynchronizationAnalyzer? SyncAnalyzer { get; }

		/// <summary>
		/// The tool's own approval interface, or <see langword="null"/> when the generic consent panel
		/// handles both decisions.
		/// </summary>
		IToolApprovalProvider? Approval { get; }

		/// <summary>
		/// Creates the execution context for this descriptor's home scope, or <see langword="null"/> when
		/// the orchestrator builds the context itself. The provider is the most-scoped one available.
		/// </summary>
		IToolExecutionContextFactory? ContextFactory { get; }
	}
}
