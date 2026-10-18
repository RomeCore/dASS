using LLMDesktopAssistant.Tools.Execution.Domain;

namespace LLMDesktopAssistant.Agents.Tasks
{
	public class AgentToolCallConfirmationRequest : NotifyPropertyChanged
	{
		/// <summary>
		/// The tool call that the agent is about to execute.
		/// </summary>
		public required AgentToolCall ToolCall { get; init; }

		/// <summary>
		/// The source that will be used to confirm the tool call.
		/// </summary>
		public required TaskCompletionSource<ToolApprovalResult> UserConfirmationSource { get; init; }
	}
}
