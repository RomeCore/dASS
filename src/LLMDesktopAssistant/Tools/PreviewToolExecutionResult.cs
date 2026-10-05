using LLMDesktopAssistant.LLM.MVVM.Additional;
using Material.Icons;

namespace LLMDesktopAssistant.Tools
{
	/// <summary>
	/// Represents the result of a tool's pre-execution check.
	/// </summary>
	public class PreviewToolExecutionResult
	{
		/// <summary>
		/// The status icon to be displayed. This will be shown next to the main title (that contains tool name).
		/// </summary>
		public VisualIconKind? StatusIcon { get; init; }

		/// <summary>
		/// The title of the status that will be shown next to the main title (that contains tool name).
		/// </summary>
		public string? StatusTitle { get; init; }

		/// <summary>
		/// Specifies whether the tool preview execution was successful or not.
		/// If specified, tool will not be executed and finished immediately.
		/// </summary>
		public bool? InterruptingSuccess { get; init; }

		/// <summary>
		/// Specifies the content to put into result content of tool call.
		/// </summary>
		public string? InterruptingContent { get; init; }

		/// <summary>
		/// Specifies the additional data to put into the tool call result.
		/// </summary>
		public ImmutableList<AdditionalChatData> InterruptingAdditionalData { get; init; } = [];

		/// <summary>
		/// Whether use markdown rendering for <see cref="InterruptingContent"/>.
		/// </summary>
		public bool UseMarkdown { get; init; }

		/// <summary>
		/// Specifies whether the tool overrides the standard HITL pipeline with its own policy decisions.
		/// When <see langword="null"/> the <see cref="ToolInfo.SelfHandledDecisions"/> will be used instead.
		/// </summary>
		public ToolPolicyDecision? SelfHandledDecisions { get; init; }

		/// <summary>
		/// The expected behaviour of the tool during execution.
		/// </summary>
		public ToolBehaviour? ExpectedBehaviour { get; init; }
	}
}
