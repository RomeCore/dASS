using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.Tools;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.Tools.Execution.Hooks
{
	/// <summary>
	/// The event vocabulary of the tool pipeline: the names a hook matches with <c>hook-type</c>, and the
	/// name it matches with <c>hook-name</c>.
	/// </summary>
	/// <remarks>
	/// The event suffix is the invocation source (<c>chat-run</c>, <c>command</c>, ...), the name prefix is the
	/// tool's origin (<c>native</c>, <c>scriptable</c>, <c>mcp</c>, <c>ad-hoc</c>). Both are matched by anchored
	/// globs, so <c>tool.result.*.error</c> and <c>native.fs-*</c> work, while a bare <c>tool.result</c> matches
	/// nothing.
	/// </remarks>
	public static class ToolHookEvents
	{
		/// <summary>
		/// The base of every event fired before a call runs.
		/// </summary>
		public const string Start = "tool.start";

		/// <summary>
		/// The base of every event fired when a call has its final result.
		/// </summary>
		public const string Result = "tool.result";

		/// <summary>
		/// The base of every event fired when a call reaches the confirmation stage.
		/// </summary>
		public const string Confirm = "tool.confirm";

		/// <summary>
		/// Composes the event fired before a call runs.
		/// </summary>
		/// <param name="source">The invocation source.</param>
		/// <returns>For example <c>tool.start.chat-run</c>.</returns>
		public static string ComposeStart(ToolInvocationSource source) =>
			Start + "." + DescribeSource(source);

		/// <summary>
		/// Composes the event fired when a call has its final result.
		/// </summary>
		/// <param name="source">The invocation source.</param>
		/// <param name="status">The terminal status of the call.</param>
		/// <returns>For example <c>tool.result.command.rejected</c>.</returns>
		public static string ComposeResult(ToolInvocationSource source, ToolStatus status) =>
			Result + "." + DescribeSource(source) + "." + DescribeStatus(status);

		/// <summary>
		/// Composes the event fired when a call is about to be confirmed, carrying the decision the pipeline
		/// arrived with.
		/// </summary>
		/// <param name="source">The invocation source.</param>
		/// <param name="decision">The pending decision of the pipeline.</param>
		/// <returns>For example <c>tool.confirm.chat-run.ask</c>.</returns>
		public static string ComposeConfirm(ToolInvocationSource source, ToolPolicyDecision decision) =>
			Confirm + "." + DescribeSource(source) + "." + DescribeDecision(decision);

		/// <summary>
		/// Composes the name a hook matches with <c>hook-name</c>: the origin of the tool and its name.
		/// </summary>
		/// <param name="descriptor">The tool, or the missing-tool descriptor.</param>
		/// <param name="call">The call, which still carries the name the model emitted.</param>
		/// <returns>For example <c>native.fs-edit</c>.</returns>
		public static string ComposeName(IToolDescriptor descriptor, IToolCall call) =>
			DescribeOrigin(descriptor.Source) + "." +
			(string.IsNullOrEmpty(descriptor.Name) ? call.ToolName : descriptor.Name);

		/// <summary>
		/// Renders the invocation source in kebab case.
		/// </summary>
		/// <param name="source">The invocation source.</param>
		/// <returns>For example <c>agent-task</c>.</returns>
		public static string DescribeSource(ToolInvocationSource source) =>
			KebabEnumNames<ToolInvocationSource>.Serialize(source) ?? source.ToString().ToLowerInvariant();

		/// <summary>
		/// Renders the terminal status in kebab case.
		/// </summary>
		public static string DescribeStatus(ToolStatus status) =>
			KebabEnumNames<ToolStatus>.Serialize(status) ?? status.ToString().ToLowerInvariant();

		/// <summary>
		/// Renders the pending pipeline decision in kebab case.
		/// </summary>
		/// <param name="decision">The decision.</param>
		/// <returns>For example <c>ask</c>.</returns>
		public static string DescribeDecision(ToolPolicyDecision decision) =>
			KebabEnumNames<ToolPolicyDecision>.Serialize(decision) ?? decision.ToString().ToLowerInvariant();

		/// <summary>
		/// Renders the layer that produced a decision in kebab case, or <see langword="null"/> when no layer
		/// did.
		/// </summary>
		/// <param name="source">The layer, or <see langword="null"/>.</param>
		/// <returns>For example <c>memorization</c>.</returns>
		public static string? DescribeReasonSource(ToolDecisionSource? source) =>
			source is { } value ? KebabEnumNames<ToolDecisionSource>.Serialize(value) : null;

		/// <summary>
		/// Renders the origin of a tool from the behaviour flags its descriptor declares.
		/// </summary>
		public static string DescribeOrigin(ToolSource source)
		{
			return source switch
			{
				ToolSource.Native => "native",
				ToolSource.Script => "script",
				ToolSource.MCP => "mcp",
				ToolSource.AdHoc => "ad-hoc",
				_ => "unknown"
			};
		}

		/// <summary>
		/// Renders behaviour flags as kebab-case names, dropping the two members that are not flags:
		/// <see cref="ToolBehaviour.None"/> is set on every value and <see cref="ToolBehaviour.All"/> is set
		/// only when every flag is.
		/// </summary>
		/// <param name="behaviour">The behaviour flags of the call.</param>
		/// <returns>The names, or <see langword="null"/> when nothing is declared.</returns>
		public static string[]? DescribeBehaviour(ToolBehaviour behaviour)
		{
			var names = Enum.GetValues<ToolBehaviour>()
				.Where(flag => flag is not ToolBehaviour.None and not ToolBehaviour.All)
				.Where(flag => behaviour.HasFlag(flag))
				.Select(flag => KebabEnumNames<ToolBehaviour>.Names[flag])
				.ToArray();

			return names.Length == 0 ? null : names;
		}
	}
}
