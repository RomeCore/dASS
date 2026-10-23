using LiveMarkdown.Avalonia;
using LLMDesktopAssistant.Controls.Markdown;
using LLMDesktopAssistant.Controls.Markdown.CodeBlocks;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Services;
using Serilog;

namespace LLMDesktopAssistant.Desktop.Markdown
{
	/// <summary>
	/// Gives a <c>```mermaid</c> fence its diagram tab.
	/// </summary>
	/// <remarks>
	/// Replaces the old markdown node: mermaid is no longer a block type of its own, it is an ordinary fenced block
	/// with a preview provider - so the source, the copy/save buttons and the split view all work the same way as
	/// for any other language.
	/// </remarks>
	[Service(typeof(ICodeBlockPreviewProvider))]
	public sealed class MermaidPreviewProvider : ICodeBlockPreviewProvider
	{
		/// <inheritdoc />
		public MarkdownExtensionScope Scope => MarkdownExtensionScope.Message;

		/// <inheritdoc />
		public ICodeBlockPreview? TryCreatePreview(MarkdownCodeBlockContext context)
			=> string.Equals(context.Content.Language.Trim(), "mermaid", StringComparison.OrdinalIgnoreCase)
				? new MermaidPreview()
				: null;
	}

	/// <summary>
	/// The diagram tab of one mermaid block: a single presenter re-fed with the source on every update, so the pan
	/// and zoom state survives streaming.
	/// </summary>
	public sealed class MermaidPreview : ICodeBlockPreview
	{
		private readonly MermaidPresenter _presenter;

		/// <summary>
		/// Creates the preview and its control, once per block.
		/// </summary>
		public MermaidPreview()
		{
			_presenter = new MermaidPresenter { Classes = { "MermaidBlock" } };
			Content = new PanAndZoom
			{
				Classes = { "MermaidBlock" },
				Content = _presenter,
			};
		}

		/// <inheritdoc />
		public LocaleKeyBase Title => Locale.GetKey("common.diagram");

		/// <inheritdoc />
		public bool IsDefault => true;

		/// <inheritdoc />
		public bool IsShownSplitByDefault => true;

		/// <inheritdoc />
		public object Content { get; }

		/// <inheritdoc />
		public ValueTask UpdateAsync(CodeBlockContent content, bool isFinal, CancellationToken cancellationToken)
		{
			if (isFinal)
			{
				_presenter.Text = content.Text;
				Log.Information("Mermaid diagram updated: {Content}", content.Text);
			}
			return ValueTask.CompletedTask;
		}

		/// <inheritdoc />
		public void Dispose()
		{
		}
	}
}
