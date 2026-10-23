using LiveMarkdown.Avalonia;
using LLMDesktopAssistant.Markdown.Parsers;
using LLMDesktopAssistant.Markdown.UINodes;
using LLMDesktopAssistant.Services;
using Markdig.Extensions.Alerts;
using Markdig;

namespace LLMDesktopAssistant.Markdown;

/// <summary>
/// Configures the Markdown pipeline with custom extensions
/// for quick actions, quick explanations, and other LLM-specific syntax.
/// </summary>
[Service]
public class MarkdownAdditionalNodesConfigurator
{
	public MarkdownAdditionalNodesConfigurator()
	{
		MarkdownRenderer.ConfigurePipeline += ConfigurePipeline;

		MarkdownNode.Register<QuickActionUiNode>();
		MarkdownNode.Register<QuickExplanationUiNode>();

		// Fenced code blocks render through our node: it owns the preview tabs, the split view and the actions.
		MarkdownNode.Register<ExtendedCodeBlockNode>();

		MarkdownNode.Edit(builder => builder
			.Unregister<AlertBlockNode>()
			.Register<AlertBlockUiNode>()
			// Fenced blocks are handled by PreviewCodeBlockNode; the stock node is dropped so the two cannot
			// fight over a block. TODO: indented code blocks (a CodeBlock without a fence) are left without a
			// node until the nested fence parser is moved onto them.
			.Unregister<CodeBlockNode>());

		AsyncImageLoader.DefaultCache = new RamBasedAsyncImageLoaderCache();
	}

	private static void ConfigurePipeline(Markdig.MarkdownPipelineBuilder pipeline)
	{
		// Equal-length fenced code blocks cannot nest in CommonMark, so a ```csharp snippet inside a
		// ```markdown block closes the outer block early and the rest of the message turns into code.
		pipeline.UseNestedFencedCodeBlocks();

		var linkParserIndex = pipeline.InlineParsers.FindIndex(p => p is Markdig.Parsers.Inlines.LinkInlineParser);

		// Replace the stock GFM alert parser with the extended one that also supports
		// custom titles and colors: [!Note:Title], [!Important message:#FF98D8], etc.
		var richAlertParser = new RichAlertInlineParser();
		var alertParserIndex = pipeline.InlineParsers.FindIndex(p => p is AlertInlineParser);
		if (alertParserIndex >= 0)
			pipeline.InlineParsers[alertParserIndex] = richAlertParser; // swap in place
		else if (linkParserIndex >= 0)
			pipeline.InlineParsers.Insert(linkParserIndex, richAlertParser);
		else
			pipeline.InlineParsers.Add(richAlertParser);

		if (linkParserIndex >= 0)
			pipeline.InlineParsers.Insert(linkParserIndex, new QuickActionParser());
		else
			pipeline.InlineParsers.Add(new QuickActionParser());

		if (linkParserIndex >= 0)
			pipeline.InlineParsers.Insert(linkParserIndex, new QuickExplanationParser());
		else
			pipeline.InlineParsers.Add(new QuickExplanationParser());
	}
}
