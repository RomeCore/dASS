using Avalonia.Controls;
using Avalonia.Input.Platform;
using LLMDesktopAssistant.Localization;
using Material.Icons;

namespace LLMDesktopAssistant.Controls.Markdown.Selection
{
	/// <summary>
	/// Copies the selected text to the clipboard as plain text - the same text the renderer's own
	/// <c>Ctrl+C</c> produces, taken from the snapshot instead of the live selection.
	/// </summary>
	public sealed class CopySelectionProcessor : IMarkdownSelectionProcessor
	{
		/// <summary>
		/// The single stateless instance, safe to share between every message.
		/// </summary>
		public static CopySelectionProcessor Instance { get; } = new();

		private CopySelectionProcessor()
		{
		}

		/// <inheritdoc />
		public LocaleKeyBase Title => Locale.GetKey("message.selection.copy");

		/// <inheritdoc />
		public VisualIconKind Icon => MaterialIconKind.ContentCopy;

		/// <inheritdoc />
		public int Order => 0;

		/// <inheritdoc />
		public async Task ExecuteAsync(MarkdownSelectionContext context, CancellationToken cancellationToken = default)
		{
			if (string.IsNullOrEmpty(context.Text))
				return;

			if (TopLevel.GetTopLevel(context.Renderer)?.Clipboard is { } clipboard)
				await clipboard.SetTextAsync(context.Text);
		}
	}
}
