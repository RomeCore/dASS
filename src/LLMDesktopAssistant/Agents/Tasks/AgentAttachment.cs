using LLMDesktopAssistant.Utils.Attachments;

namespace LLMDesktopAssistant.Agents.Tasks
{
	/// <summary>
	/// The task world's attachment base: a payload a task object carries, projected from the neutral
	/// chat attachment model. Native payloads and text contributions are the two concrete kinds.
	/// </summary>
	public abstract class AgentAttachment : IChatAttachment
	{
		/// <summary>
		/// The source of the attachment, if applicable. This can be any object that provides additional
		/// context or metadata about the attachment.
		/// </summary>
		public object? Source { get; init; }
	}
}
