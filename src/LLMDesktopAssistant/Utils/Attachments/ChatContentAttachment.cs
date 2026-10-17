namespace LLMDesktopAssistant.Utils.Attachments
{
	/// <summary>
	/// A text contribution a tool or a stage writes onto a call: the model receives it as content
	/// appended after the call's result.
	/// </summary>
	/// <param name="content">The text contribution.</param>
	public sealed class ChatContentAttachment(string content) : IContentChatAttachment
	{
		/// <inheritdoc/>
		public string Content { get; } = content ?? throw new ArgumentNullException(nameof(content));
	}
}
