namespace LLMDesktopAssistant.Controls.Markdown.Selection
{
	/// <summary>
	/// Where a provider contributes its processors. A provider serves one kind of surface, so the consumer asks
	/// for the set its surface needs instead of enumerating every provider by hand.
	/// </summary>
	/// <remarks>
	/// <see cref="Default"/> is <c>0</c> on purpose: it means "everywhere", so it can never be matched by a bit
	/// test - see <see cref="MarkdownSelectionProcessorProviders.ForScope"/>.
	/// </remarks>
	[Flags]
	public enum MarkdownSelectionProcessorScope
	{
		/// <summary>
		/// The provider applies to every selection toolbar, wherever it is hosted.
		/// </summary>
		Default = 0,

		/// <summary>
		/// The provider applies to the markdown of a chat message (text and reasoning parts).
		/// </summary>
		Message = 1 << 0,

		/// <summary>
		/// The provider applies to the markdown of a tool call (its result).
		/// </summary>
		ToolCall = 1 << 1,
	}
}
