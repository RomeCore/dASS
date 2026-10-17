namespace LLMDesktopAssistant.Utils.Attachments
{
	/// <summary>
	/// The kind of a native attachment. It replaces the former tool- and task-specific enums: both worlds
	/// speak the same kind.
	/// </summary>
	public enum AttachmentKind
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
}
