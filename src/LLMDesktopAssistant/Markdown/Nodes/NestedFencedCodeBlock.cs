using Markdig.Parsers;
using Markdig.Syntax;

namespace LLMDesktopAssistant.Markdown.Nodes;

/// <summary>
/// A <see cref="FencedCodeBlock"/> that also remembers how many nested fenced blocks are currently
/// open inside it. Nesting is pure bookkeeping - nested code stays part of the outer block's text,
/// because a code block is a leaf and cannot hold child blocks.
/// </summary>
public class NestedFencedCodeBlock(BlockParser parser) : FencedCodeBlock(parser)
{
	/// <summary>
	/// Gets or sets the number of nested fenced blocks that are open inside this block.
	/// </summary>
	public int NestingDepth { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the opening fence line was terminated in the source, so its info
	/// string (and with it the language) can no longer grow.
	/// </summary>
	/// <remarks>
	/// A streaming document can end in the middle of the opening line: Markdig still opens the block and takes the
	/// info string up to the end of what it has (see <c>FencedBlockParserBase.DefaultInfoParser</c>), so the very
	/// first parse of a block can see <c>Info = "merm"</c> for what will become <c>```mermaid</c>. Code block
	/// extensions are built only once this flag is set (or the message has stopped streaming), which is what keeps
	/// a preview tab from being created for the wrong language.
	/// </remarks>
	public bool IsInfoComplete { get; set; }
}
