namespace LLMDesktopAssistant.Tools.Execution.Domain
{
	/// <summary>
	/// The kind of a neutral tool attachment. It mirrors the attachment kinds the two worlds can send
	/// to the model.
	/// </summary>
	public enum ToolAttachmentKind
	{
		/// <summary>
		/// An image.
		/// </summary>
		Image,

		/// <summary>
		/// An audio clip.
		/// </summary>
		Audio,

		/// <summary>
		/// A video clip.
		/// </summary>
		Video
	}

	/// <summary>
	/// A neutral attachment a tool produces. The call projects it into the world's native form: a chat
	/// attachment part, or the agent task's <c>AgentAttachment</c>. The call owns the projection, so a
	/// tool never builds a world-specific attachment itself.
	/// </summary>
	public sealed class ToolAttachment
	{
		/// <summary>
		/// The kind of the attachment. Selects the native attachment type in the invoking world.
		/// </summary>
		public required ToolAttachmentKind Kind { get; init; }

		/// <summary>
		/// The attachment URL. For generated content it already carries base64 data
		/// (<c>data:image/png;base64,...</c>), so the call does not need the bytes.
		/// </summary>
		public required string Url { get; init; }
	}
}
