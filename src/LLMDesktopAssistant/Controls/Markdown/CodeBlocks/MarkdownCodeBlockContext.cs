using LiveMarkdown.Avalonia;

namespace LLMDesktopAssistant.Controls.Markdown.CodeBlocks
{
	/// <summary>
	/// Everything a code block extension sees about the block it is asked for: the content snapshot, the renderer
	/// it lives in, and the domain object that owns the markdown.
	/// </summary>
	/// <remarks>
	/// The context is built by the code block itself in the moment it needs an answer, so a provider never holds a
	/// live control and never has to find chats, services or messages through the visual tree.
	/// </remarks>
	public sealed record MarkdownCodeBlockContext
	{
		/// <summary>
		/// Gets the content snapshot of the block.
		/// </summary>
		public required CodeBlockContent Content { get; init; }

		/// <summary>
		/// Gets the renderer the block belongs to (its top level hosts the clipboard and the popup).
		/// </summary>
		public required MarkdownRenderer Renderer { get; init; }

		/// <summary>
		/// Gets the target meta-object that owns the markdown content: a message, a tool call, etc.
		/// </summary>
		public required object? MarkdownOwner { get; init; }
	}
}
