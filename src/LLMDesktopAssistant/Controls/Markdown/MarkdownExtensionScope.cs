namespace LLMDesktopAssistant.Controls.Markdown
{
	/// <summary>
	/// Where a markdown extension provider contributes its items. A provider serves one kind of surface,
	/// so the consumer asks for the set its surface needs instead of enumerating every provider by hand.
	/// </summary>
	/// <remarks>
	/// <see cref="Default"/> is <c>0</c> on purpose: it means "everywhere", so it can never be matched by a bit
	/// test - see <see cref="MarkdownExtensionProviders"/>.
	/// </remarks>
	[Flags]
	public enum MarkdownExtensionScope
	{
		/// <summary>
		/// The provider applies to every markdown surface, wherever it is hosted.
		/// </summary>
		Default = 0,

		/// <summary>
		/// The provider applies to the markdown of a chat message (text and reasoning parts).
		/// </summary>
		Message = 1 << 0,

		/// <summary>
		/// The provider applies to the markdown of a tool call (its arguments and its result).
		/// </summary>
		ToolCall = 1 << 1,
	}
}
