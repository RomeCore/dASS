using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.Agents.Tasks
{
	public class AgentAssistantMessage : AgentChatMessage, IToolCallHost
	{
		private string? _reasoningContent;
		/// <summary>
		/// The reasoning content of the assistant's message.
		/// </summary>
		public string? ReasoningContent
		{
			get => _reasoningContent;
			set => SetProperty(ref _reasoningContent, value);
		}

		private RangeObservableCollection<AgentAttachment> _attachments = [];
		/// <summary>
		/// A collection of attachments associated with the assistant's message.
		/// </summary>
		public RangeObservableCollection<AgentAttachment> Attachments
		{
			get => _attachments;
			set => _attachments.Reset(value);
		}

		private RangeObservableCollection<AgentToolCall> _toolCalls = [];
		/// <summary>
		/// A collection of tool calls associated with the assistant's message.
		/// </summary>
		public RangeObservableCollection<AgentToolCall> ToolCalls
		{
			get => _toolCalls;
			set => _toolCalls.Reset(value);
		}

		IReadOnlyList<IToolCall> IToolCallHost.ToolCalls => ToolCalls;

		private AgentUsageStatistics? _usageStatistics;
		/// <summary>
		/// The usage statistics for this assistant message.
		/// </summary>
		public AgentUsageStatistics? UsageStatistics
		{
			get => _usageStatistics;
			set => SetProperty(ref _usageStatistics, value);
		}
	}
}
