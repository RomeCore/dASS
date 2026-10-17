namespace LLMDesktopAssistant.Tools.Execution.Domain
{
	/// <summary>
	/// The message that owns a group of tool calls — a chat message (assistant or user) or an agent
	/// task's assistant message. The core needs nothing else from the host.
	/// </summary>
	public interface IToolCallHost
	{
		/// <summary>
		/// Every tool call that lives on this message, in order. The list is not filtered: a consumer
		/// that needs the calls awaiting approval (cascade) checks their status itself; the approval
		/// helper uses the whole list as context.
		/// </summary>
		IReadOnlyList<IToolCall> ToolCalls { get; }
	}
}
