using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Services;

namespace LLMDesktopAssistant.Controls.Markdown.CodeBlocks
{
	/// <summary>
	/// Gives an <c>```axaml</c> fence a tab that renders the sample as a live control.
	/// </summary>
	/// <remarks>
	/// Built on <see cref="DynamicAxamlControl"/>, which reloads its content whenever <c>Axaml</c> changes - so the
	/// preview follows the source while it streams. A broken fragment is shown as the loader's diagnostic
	/// placeholder instead of taking the message down.
	/// </remarks>
	[Service(typeof(ICodeBlockPreviewProvider))]
	public sealed class AxamlPreviewProvider : ICodeBlockPreviewProvider
	{
		/// <inheritdoc />
		public int Order => 20;

		/// <inheritdoc />
		public ICodeBlockPreview? TryCreatePreview(MarkdownCodeBlockContext context)
			=> IsAxaml(context.Content.Language) ? new AxamlPreview() : null;

		private static bool IsAxaml(string? language)
		{
			var name = language?.Trim();
			return string.Equals(name, "axaml", StringComparison.OrdinalIgnoreCase)
				|| string.Equals(name, "xaml", StringComparison.OrdinalIgnoreCase);
		}
	}

	/// <summary>
	/// The live tab of one AXAML block: the sample itself, re-loaded on every source change.
	/// </summary>
	public sealed class AxamlPreview : ICodeBlockPreview
	{
		private readonly DynamicAxamlControl _control;

		/// <summary>
		/// Creates the preview and its control, once per block.
		/// </summary>
		public AxamlPreview()
		{
			_control = new DynamicAxamlControl
			{
				Margin = new Thickness(10)
			};
			Content = new ScrollViewer
			{
				Content = _control,
				Padding = new Thickness(10),
				VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
				HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
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
			// Only the finished fragment is loaded: a half-typed document is a parse error more often than not, and
			// reloading XAML on every streaming chunk costs far more than it shows.
			if (!isFinal) return ValueTask.CompletedTask;

			if (!string.Equals(_control.Axaml, content.Text, StringComparison.Ordinal))
				_control.Axaml = content.Text;

			return ValueTask.CompletedTask;
		}

		/// <inheritdoc />
		public void Dispose()
		{
		}
	}
}
