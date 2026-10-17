using Markdig;
using Markdig.Parsers;
using Markdig.Renderers;
using Markdig.Syntax;

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
internal sealed class NestedFencedCodeBlocksExtension : IMarkdownExtension
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

/// <summary>
/// A <see cref="FencedCodeBlock"/> that also remembers how many nested fenced blocks are currently
/// open inside it. Nesting is pure bookkeeping - nested code stays part of the outer block's text,
/// because a code block is a leaf and cannot hold child blocks.
/// </summary>
internal sealed class NestedFencedCodeBlock : FencedCodeBlock
{
	public NestedFencedCodeBlock(BlockParser parser) : base(parser)
	{
	}

	/// <summary>
	/// Gets or sets the number of nested fenced blocks that are open inside this block.
	/// </summary>
	public int NestingDepth { get; set; }
}

/// <summary>
/// <see cref="FencedCodeBlockParser"/> with nested fence support - see
/// <see cref="NestedFencedCodeBlocksExtensions"/> for the rules.
/// </summary>
public sealed class NestedFencedCodeBlockParser : FencedCodeBlockParser
{
	/// <summary>
	/// Nesting deeper than this is left to the stock behaviour (it only ever matters for
	/// pathological input).
	/// </summary>
	private const int MaxNestingDepth = 8;

	private readonly FencedCodeBlockParser _fallback;

	public NestedFencedCodeBlockParser() : this(null)
	{
	}

	internal NestedFencedCodeBlockParser(FencedCodeBlockParser? fallback)
	{
		_fallback = fallback is null or NestedFencedCodeBlockParser ? new FencedCodeBlockParser() : fallback;

		// Inherit the configuration of the parser we decorate instead of hardcoding the defaults.
		InfoParser = _fallback.InfoParser;
		InfoPrefix = _fallback.InfoPrefix;
		TryParseAttributes = _fallback.TryParseAttributes;
		MinimumMatchCount = _fallback.MinimumMatchCount;
		MaximumMatchCount = _fallback.MaximumMatchCount;
	}

	protected override FencedCodeBlock CreateFencedBlock(BlockProcessor processor)
	{
		var block = new NestedFencedCodeBlock(this)
		{
			IndentCount = processor.Indent,
		};

		if (processor.TrackTrivia)
		{
			block.LinesBefore = processor.TakeLinesBefore();
			block.TriviaBefore = processor.UseTrivia(processor.Start - 1);
			block.NewLine = processor.Line.NewLine;
		}

		return block;
	}

	public override BlockState TryContinue(BlockProcessor processor, Block block)
	{
		if (block is NestedFencedCodeBlock fence && TryContinueNested(processor, fence))
		{
			ExtendSpans(processor, block);
			return BlockState.Continue;
		}

		var state = _fallback.TryContinue(processor, block);
		ExtendSpans(processor, block);
		return state;
	}

	/// <summary>
	/// Handles one line of a fenced block that belongs to a nested fenced block. The line is kept as
	/// content - only the depth counter changes. Returns <c>false</c> when the line is not a nested
	/// fence and the stock parser has to make the decision.
	/// </summary>
	private static bool TryContinueNested(BlockProcessor processor, NestedFencedCodeBlock fence)
	{
		// A line indented by 4+ spaces can never open or close a fence.
		if (processor.IsCodeIndent)
			return false;

		var line = processor.Line;
		if (line.CurrentChar != fence.FencedChar)
			return false;

		var span = line.AsSpan();
		var fenceLength = CountLeading(span, fence.FencedChar);

		// Shorter fences cannot close the enclosing block, so they are plain content for it and
		// nothing has to be balanced (this is what keeps ````-wrapped ``` snippets untouched).
		if (fenceLength < fence.OpeningFencedCharCount)
			return false;

		var rest = span[fenceLength..].TrimStart();
		var hasInfo = !rest.IsEmpty && (fence.FencedChar != '`' || rest.IndexOf('`') < 0);

		if (hasInfo)
		{
			// A fence with an info string cannot close anything, so strict CommonMark keeps it as
			// text. Treat it as the opening of a nested block - but only when the document really
			// contains fences that balance it, otherwise a stray ```lang line would swallow the
			// rest of the document.
			if (fence.NestingDepth >= MaxNestingDepth)
				return false;
			if (!IsBalancedFrom(line.Text, line.End + 1, fence.FencedChar, fence.OpeningFencedCharCount))
				return false;

			fence.NestingDepth++;
		}
		else
		{
			// A bare fence closes the innermost block: the nested ones first, then the enclosing
			// block itself, which is left to the stock parser.
			if (fence.NestingDepth == 0)
				return false;

			fence.NestingDepth--;
		}

		// The line stays inside the block as content.
		processor.GoToColumn(processor.ColumnBeforeIndent);
		RemoveBlockIndent(processor, fence);
		return true;
	}

	/// <summary>
	/// Replicates the stock parser's content indentation removal for a line we keep ourselves.
	/// </summary>
	private static void RemoveBlockIndent(BlockProcessor processor, FencedCodeBlock fence)
	{
		if (processor.TrackTrivia)
			return;

		var c = processor.CurrentChar;
		var indentCount = fence.IndentCount;
		while (indentCount > 0 && c is ' ' or '\t')
		{
			indentCount--;
			c = processor.NextChar();
		}
	}

	/// <summary>
	/// Keeps a block's source span covering every line it consumes, exactly like LiveMarkdown's code
	/// block span fixer does on the stock path (that fixer is bypassed for lines we keep ourselves).
	/// </summary>
	private static void ExtendSpans(BlockProcessor processor, Block block)
	{
		var start = processor.Line.Start;
		var end = processor.Line.End;

		for (var current = block; current is not null; current = current.Parent)
		{
			current.Span = new SourceSpan(
				Math.Min(current.Span.Start, start),
				Math.Max(current.Span.End, end));
		}
	}

	/// <summary>
	/// Simulates the depth counting over the raw text that follows, starting with the candidate
	/// nested block already open. Returns <c>true</c> when that nested block and the enclosing block
	/// are both closed further down the document.
	/// </summary>
	private static bool IsBalancedFrom(string? text, int start, char fenceChar, int fenceLength)
	{
		if (text is null || start >= text.Length)
			return false;

		var depth = 1;
		var i = start;

		while (i < text.Length)
		{
			// Skip the newline sequence that separates us from the next line.
			while (i < text.Length && text[i] is '\r' or '\n')
				i++;
			if (i >= text.Length)
				return false;

			var lineEnd = i;
			while (lineEnd < text.Length && text[lineEnd] is not ('\r' or '\n'))
				lineEnd++;

			switch (ClassifyFenceLine(text.AsSpan(i, lineEnd - i), fenceChar, fenceLength))
			{
				case FenceLine.Bare:
					if (depth == 0)
						return true; // the enclosing block is closed here
					depth--;
					break;
				case FenceLine.WithInfo:
					depth++;
					break;
			}

			i = lineEnd;
		}

		return false;
	}

	/// <summary>
	/// Classifies a raw source line as a fence line usable for the balance simulation.
	/// </summary>
	private static FenceLine ClassifyFenceLine(ReadOnlySpan<char> line, char fenceChar, int fenceLength)
	{
		// Up to 3 leading spaces are allowed before a fence, 4+ makes it code-indented content.
		var indent = 0;
		while (indent < line.Length && line[indent] == ' ')
			indent++;
		if (indent > 3)
			return FenceLine.None;

		var body = line[indent..];
		var length = CountLeading(body, fenceChar);
		if (length < fenceLength)
			return FenceLine.None;

		var rest = body[length..].TrimStart();
		if (rest.IsEmpty)
			return FenceLine.Bare;

		// An info string of a backtick fence may not contain backticks, otherwise it is not a fence.
		if (fenceChar == '`' && rest.IndexOf('`') >= 0)
			return FenceLine.None;

		return FenceLine.WithInfo;
	}

	private static int CountLeading(ReadOnlySpan<char> span, char c)
	{
		var count = 0;
		while (count < span.Length && span[count] == c)
			count++;
		return count;
	}

	private enum FenceLine
	{
		None,
		Bare,
		WithInfo,
	}
}
