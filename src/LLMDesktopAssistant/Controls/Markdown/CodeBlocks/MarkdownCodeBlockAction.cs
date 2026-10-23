using LLMDesktopAssistant.Controls.Icons;
using LLMDesktopAssistant.Localization;

namespace LLMDesktopAssistant.Controls.Markdown.CodeBlocks
{
	/// <summary>
	/// Base class for code block actions: the observable state a button binds to, so an implementation only
	/// writes the title, the icon and the work.
	/// </summary>
	public abstract class MarkdownCodeBlockAction : NotifyPropertyChanged, IMarkdownCodeBlockAction
	{
		private bool _isVisible = true;

		/// <inheritdoc />
		public bool IsVisible
		{
			get => _isVisible;
			protected set => SetProperty(ref _isVisible, value);
		}

		private VisualIconKind _icon;

		/// <inheritdoc />
		public VisualIconKind Icon
		{
			get => _icon;
			protected set => SetProperty(ref _icon, value);
		}

		private object? _additionalContent;

		/// <inheritdoc />
		public object? AdditionalContent
		{
			get => _additionalContent;
			protected set => SetProperty(ref _additionalContent, value);
		}

		/// <inheritdoc />
		public abstract LocaleKeyBase Title { get; }

		/// <inheritdoc />
		public virtual int Order => 0;

		/// <inheritdoc />
		public abstract Task ExecuteAsync(MarkdownCodeBlockContext context, CancellationToken cancellationToken);
	}
}
