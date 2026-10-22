using LiveMarkdown.Avalonia;

namespace LLMDesktopAssistant.Controls.Markdown.Selection
{
	/// <summary>
	/// One selected range inside a single markdown text block. <see cref="Start"/> and <see cref="Length"/> are
	/// UTF-16 offsets local to the block's layout text (the same coordinates the library's
	/// <c>GetTextRangeBoundsInControl</c> and text search use), and <see cref="Text"/> is the selected text
	/// of that block as the user sees it.
	/// </summary>
	public readonly record struct MarkdownSelectedRange(MarkdownTextBlock Block, int Start, int Length, string Text);
}
