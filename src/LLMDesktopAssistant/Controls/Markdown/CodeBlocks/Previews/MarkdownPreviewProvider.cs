using Avalonia;
using Avalonia.Controls;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Services;

namespace LLMDesktopAssistant.Controls.Markdown.CodeBlocks
{
	/// <summary>
	/// Gives a <c>```markdown</c> fence a tab with the block rendered as markdown.
	/// </summary>
	/// <remarks>
	/// The preview is another <see cref="MarkdownControl"/> without providers of its own: a nested fence inside the
	/// sample renders as plain source, so a markdown sample showing a markdown sample cannot recurse.
	/// </remarks>
	[Service(typeof(ICodeBlockPreviewProvider))]
	public sealed class MarkdownPreviewProvider : ICodeBlockPreviewProvider
	{
		/// <inheritdoc />
		public int Order => 10;

		/// <inheritdoc />
		public ICodeBlockPreview? TryCreatePreview(MarkdownCodeBlockContext context)
			=> IsMarkdown(context.Content.Language) ? new MarkdownPreview() : null;

		private static bool IsMarkdown(string? language)
		{
			var name = language?.Trim();
			return string.Equals(name, "markdown", StringComparison.OrdinalIgnoreCase)
				|| string.Equals(name, "md", StringComparison.OrdinalIgnoreCase)
				|| string.Equals(name, "mdx", StringComparison.OrdinalIgnoreCase);
		}
	}

	/// <summary>
	/// The rendered tab of one markdown block: a message-like markdown surface fed with the block's text.
	/// </summary>
	public sealed class MarkdownPreview : ICodeBlockPreview
	{
		private readonly MarkdownControl _control;

		/// <summary>
		/// Creates the preview and its control, once per block.
		/// </summary>
		public MarkdownPreview()
		{
			_control = new MarkdownControl
			{
				Margin = new Thickness(10)
			};
			Content = new ScrollViewer
			{
				Content = _control,
				VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
				HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
			};
		}

		/// <inheritdoc />
		public LocaleKeyBase Title => Locale.GetKey("common.preview");

		/// <inheritdoc />
		public bool IsDefault => true;

		/// <inheritdoc />
		public bool IsShownSplitByDefault => true;

		/// <inheritdoc />
		public object Content { get; }

		/// <inheritdoc />
		public ValueTask UpdateAsync(CodeBlockContent content, bool isFinal, CancellationToken cancellationToken)
		{
			// The control appends only a growing suffix, so a streaming update costs the delta and not a reparse
			// of the whole sample.
			_control.MarkdownText = content.Text;
			return ValueTask.CompletedTask;
		}

		/// <inheritdoc />
		public void Dispose()
		{
		}
	}
}
