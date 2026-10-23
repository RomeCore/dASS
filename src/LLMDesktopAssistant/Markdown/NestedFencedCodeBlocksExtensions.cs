using Markdig;
using Markdig.Parsers;
using Markdig.Renderers;

namespace LLMDesktopAssistant.Markdown.Parsers;

/// <summary>
/// Pipeline extension that makes fenced code blocks nestable.
/// </summary>
/// <remarks>
/// CommonMark has no concept of nesting: a fenced block is closed by the first bare fence of at least
/// the same length, so the widely used (and LLM-typical) shape where a ```markdown block contains a
/// ```csharp block cannot be expressed with fences of equal length - the first bare fence closes the
/// outer block and the trailing one starts a stray new block that swallows the rest of the document.
/// <para>
/// This extension resolves that ambiguity with depth counting instead: inside an open fenced block a
/// same-character fence of at least the opening length <i>with an info string</i> opens a nested
/// block, and a <i>bare</i> fence closes the innermost open one. Fences shorter than the opening one
/// are left alone, so documents that use the canonical longer outer fence (4 backticks around 3)
/// keep parsing exactly as before.
/// </para>
/// </remarks>
public static class NestedFencedCodeBlocksExtensions
{
	/// <summary>
	/// Registers nested fenced code block support on the pipeline.
	/// </summary>
	public static MarkdownPipelineBuilder UseNestedFencedCodeBlocks(this MarkdownPipelineBuilder pipeline)
	{
		pipeline.Extensions.ReplaceOrAdd<NestedFencedCodeBlocksExtension>(new NestedFencedCodeBlocksExtension());
		return pipeline;
	}
}

/// <summary>
/// Swaps the stock <see cref="FencedCodeBlockParser"/> (or whatever parser the pipeline already
/// installed, for example LiveMarkdown's source-span fixer) for <see cref="NestedFencedCodeBlockParser"/>.
/// </summary>
public class NestedFencedCodeBlocksExtension : IMarkdownExtension
{
	public void Setup(MarkdownPipelineBuilder pipeline)
	{
		var index = pipeline.BlockParsers.FindIndex(static parser => parser is FencedCodeBlockParser);
		if (index < 0)
		{
			pipeline.BlockParsers.Add(new NestedFencedCodeBlockParser(null));
			return;
		}

		// Wrap, don't drop: the installed parser may carry behaviour we must keep
		// (LiveMarkdown replaces it with its own subclass that keeps code block source spans whole).
		pipeline.BlockParsers[index] = new NestedFencedCodeBlockParser((FencedCodeBlockParser)pipeline.BlockParsers[index]);
	}

	public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
	{
	}
}
