using Avalonia.Controls;
using Avalonia.Input.Platform;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Services;
using Material.Icons;

namespace LLMDesktopAssistant.Controls.Markdown.CodeBlocks.Actions
{
	/// <summary>
	/// Offers the copy button for every fenced code block.
	/// </summary>
	[Service(typeof(IMarkdownCodeBlockActionProvider))]
	public sealed class CopyCodeBlockActionProvider : IMarkdownCodeBlockActionProvider
	{
		/// <inheritdoc />
		public IReadOnlyList<IMarkdownCodeBlockAction> GetActions(MarkdownCodeBlockContext context)
			=> [new CopyCodeBlockAction()];
	}

	/// <summary>
	/// Copies the source of the block to the clipboard, exactly as typed - not the highlighted render.
	/// </summary>
	public sealed class CopyCodeBlockAction : MarkdownCodeBlockAction
	{
		/// <summary>
		/// Creates the action with its icon already set.
		/// </summary>
		public CopyCodeBlockAction()
		{
			Icon = MaterialIconKind.ContentCopy;
		}

		/// <inheritdoc />
		public override LocaleKeyBase Title => Locale.GetKey("common.copy");

		/// <inheritdoc />
		public override int Order => 0;

		/// <inheritdoc />
		public override async Task ExecuteAsync(MarkdownCodeBlockContext context, CancellationToken cancellationToken)
		{
			if (string.IsNullOrEmpty(context.Content.Text))
				return;

			if (TopLevel.GetTopLevel(context.Renderer)?.Clipboard is { } clipboard)
				await clipboard.SetTextAsync(context.Content.Text);
		}
	}
}
