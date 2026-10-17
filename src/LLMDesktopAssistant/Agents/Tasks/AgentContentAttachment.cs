using LLMDesktopAssistant.Utils.Attachments;

namespace LLMDesktopAssistant.Agents.Tasks
{
	/// <summary>
	/// A text contribution of the task world: the model receives it as content appended after the tool
	/// call's result.
	/// </summary>
	public sealed class AgentContentAttachment : AgentAttachment, IContentChatAttachment
	{
		/// <summary>
		/// The text contribution.
		/// </summary>
		public required string Content { get; init; }
	}
}
