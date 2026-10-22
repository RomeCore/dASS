using LiveMarkdown.Avalonia;

namespace LLMDesktopAssistant.Controls.Markdown.Selection
{
	/// <summary>
	/// An immutable snapshot of the text the user selected inside one <see cref="MarkdownRenderer"/>.
	/// </summary>
	/// <remarks>
	/// The snapshot is taken right before the selection toolbar is shown, and the processors only ever see it —
	/// never the live selection. That is what lets the toolbar clear the selection before running an action
	/// without breaking processors that read the text asynchronously.
	/// </remarks>
	public sealed record MarkdownSelectionContext
	{
		/// <summary>
		/// Gets the selected text: the per-block selections in document order, joined the same way the renderer
		/// joins them for the clipboard.
		/// </summary>
		public required string Text { get; init; }

		/// <summary>
		/// Gets the per-block ranges <see cref="Text"/> was built from, in document order.
		/// </summary>
		public required IReadOnlyList<MarkdownSelectedRange> Ranges { get; init; }

		/// <summary>
		/// Gets the renderer the selection belongs to (its top level hosts the clipboard and the popup).
		/// </summary>
		public required MarkdownRenderer Renderer { get; init; }

		/// <summary>
		/// Gets the target meta-object that owns the markdown content. This can be a message, a tool call, etc.
		/// </summary>
		public required object? MarkdownOwner { get; init; }
	}
}
