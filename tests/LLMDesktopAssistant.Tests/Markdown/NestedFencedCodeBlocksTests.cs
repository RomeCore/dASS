using LiveMarkdown.Avalonia;
using LLMDesktopAssistant.Markdown.Parsers;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace LLMDesktopAssistant.Tests.Markdown;

/// <summary>
/// Nested fenced code blocks. CommonMark closes a fenced block on the first bare fence of at least
/// the opening length, so a ```csharp block inside a ```markdown block only works when the outer
/// fence is longer - an LLM-typical equal-length shape otherwise closes the outer block early and
/// the trailing fence swallows the rest of the message.
/// <see cref="NestedFencedCodeBlocksExtensions"/> resolves that ambiguity with depth counting; these
/// tests pin both the resolved cases and the cases that must stay exactly as the stock parser sees them.
/// </summary>
public class NestedFencedCodeBlocksTests
{
	private static readonly MarkdownPipeline StockPipeline = new MarkdownPipelineBuilder()
		.UseAdvancedExtensions()
		.Build();

	private static readonly MarkdownPipeline NestedPipeline = new MarkdownPipelineBuilder()
		.UseAdvancedExtensions()
		.UseNestedFencedCodeBlocks()
		.Build();

	/// <summary>
	/// The shape LiveMarkdown's default pipeline has: its own <see cref="FencedCodeBlockParser"/> subclass
	/// (the source-span fixer) installed before ours.
	/// </summary>
	private static readonly MarkdownPipeline LiveMarkdownPipeline = new MarkdownPipelineBuilder()
		.UseAdvancedExtensions()
		.UseCodeBlockSpanFixer()
		.UseNestedFencedCodeBlocks()
		.Build();

	private const string UserSample = """
		```markdown

		Here's the sample:
		```csharp
		Print("Hello");
		```

		```
		Trailing paragraph.
		""";

	private const string UserSamplePrefix = """
		```markdown

		Here's the sample:
		```csharp
		Print("Hello");
		""";

	private const string LongerOuterFence = """
		````markdown
		```csharp
		Print("Hello");
		```
		````
		""";

	private const string UnbalancedNestedFence = """
		```csharp
		Print("x");
		```xml
		```

		More text.

		```json
		{}
		```
		""";

	private const string NestedThenMoreBlocks = """
		```markdown
		```csharp
		Print("x");
		```
		```

		Text.

		```json
		{}
		```
		""";

	private const string TildeOuterFence = """
		~~~markdown
		```csharp
		Print("x");
		```
		~~~
		""";

	[Fact]
	public void StockPipeline_SplitsAnEqualLengthNestedFenceIntoTwoBlocks()
	{
		var document = ParseStock(UserSample);

		Assert.Equal(2, document.Count);
		Assert.Equal(3, CodeBlock(document).ClosingFencedCharCount);

		// The trailing fence opens a stray block that swallows the rest of the message.
		Assert.Equal("Trailing paragraph.", Content(CodeBlock(document, 1)));
	}

	[Fact]
	public void NestedPipeline_KeepsTheOuterBlockOpen()
	{
		var document = ParseNested(UserSample);

		Assert.Equal(2, document.Count);

		var outer = CodeBlock(document);
		Assert.Equal("markdown", outer.Info);
		Assert.Equal(3, outer.ClosingFencedCharCount);
		Assert.Equal("Trailing paragraph.", Text(document, 1));
	}

	[Fact]
	public void NestedPipeline_KeepsTheNestedLinesInTheContent()
	{
		var outer = CodeBlock(ParseNested(UserSample));

		Assert.Equal(
			Lines("", "Here's the sample:", "```csharp", "Print(\"Hello\");", "```", ""),
			Content(outer));
	}

	[Fact]
	public void StreamingPrefix_DoesNotNestBeforeTheNestedFenceIsClosed()
	{
		var outer = CodeBlock(ParseNested(UserSamplePrefix));

		Assert.Equal(0, outer.ClosingFencedCharCount);
		Assert.Equal(
			Lines("", "Here's the sample:", "```csharp", "Print(\"Hello\");"),
			Content(outer));
	}

	[Fact]
	public void ShorterInnerFence_IsLeftToTheStockBehaviour()
	{
		var stock = ParseStock(LongerOuterFence);
		var nested = ParseNested(LongerOuterFence);

		Assert.Single(nested);

		var outer = CodeBlock(nested);
		Assert.Equal(4, outer.ClosingFencedCharCount);
		Assert.Equal(Content(CodeBlock(stock)), Content(outer));
	}

	[Fact]
	public void UnbalancedNestedFence_DoesNotSwallowTheRestOfTheDocument()
	{
		var stock = ParseStock(UnbalancedNestedFence);
		var nested = ParseNested(UnbalancedNestedFence);

		Assert.Equal(3, nested.Count);
		Assert.Equal(Content(CodeBlock(stock)), Content(CodeBlock(nested)));
		Assert.Equal(Lines("Print(\"x\");", "```xml"), Content(CodeBlock(nested)));
	}

	[Fact]
	public void BalancedNestedFence_LeavesTheFollowingBlocksAlone()
	{
		var document = ParseNested(NestedThenMoreBlocks);

		Assert.Equal(3, document.Count);
		Assert.Equal(Lines("```csharp", "Print(\"x\");", "```"), Content(CodeBlock(document)));
		Assert.Equal("Text.", Text(document, 1));
		Assert.Equal("{}", Content(CodeBlock(document, 2)));
	}

	[Fact]
	public void InstalledParser_IsWrappedNotReplaced()
	{
		var outer = CodeBlock(global::Markdig.Markdown.Parse(UserSample, LiveMarkdownPipeline));
		Assert.Equal(Lines("", "Here's the sample:", "```csharp", "Print(\"Hello\");", "```", ""), Content(outer));

		// LiveMarkdown's span fixer must survive the wrap: a still-open block reaches to the last char.
		var open = CodeBlock(global::Markdig.Markdown.Parse(UserSamplePrefix, LiveMarkdownPipeline));
		Assert.Equal(UserSamplePrefix.Length - 1, open.Span.End);
	}

	[Fact]
	public void TildeOuterFence_IsNotClosedByBackticks()
	{
		var outer = CodeBlock(ParseNested(TildeOuterFence));

		Assert.Equal('~', outer.FencedChar);
		Assert.Equal(3, outer.ClosingFencedCharCount);
		Assert.Equal(Lines("```csharp", "Print(\"x\");", "```"), Content(outer));
	}

	private static MarkdownDocument ParseStock(string markdown)
		=> global::Markdig.Markdown.Parse(markdown, StockPipeline);

	private static MarkdownDocument ParseNested(string markdown)
		=> global::Markdig.Markdown.Parse(markdown, NestedPipeline);

	// The nested parser produces a FencedCodeBlock subclass that carries the nesting depth.
	private static FencedCodeBlock CodeBlock(MarkdownDocument document, int index = 0)
		=> Assert.IsAssignableFrom<FencedCodeBlock>(document[index]);

	private static string Text(MarkdownDocument document, int index)
	{
		// Leaf lines of a paragraph are released after inline parsing - read the inline literal instead.
		var paragraph = Assert.IsType<ParagraphBlock>(document[index]);
		return Assert.IsType<LiteralInline>(paragraph.Inline!.FirstChild).ToString();
	}

	private static string Content(FencedCodeBlock block) => block.Lines.ToString().Replace("\r\n", "\n");

	private static string Lines(params string[] lines) => string.Join("\n", lines);
}
