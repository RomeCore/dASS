using Material.Icons;

namespace LLMDesktopAssistant.UIExtensions.CodeBlockExtensions
{
	/// <summary>
	/// Base class for markdown code block extensions.
	/// </summary>
	public abstract class CodeBlockExtension : NotifyPropertyChanged
	{
		/// <summary>
		/// The order of the extension. Lower values are displayed first in the StackPanel.
		/// </summary>
		public virtual int Order => 0;

		private VisualIconKind _icon;
		/// <summary>
		/// The icon for button associated with this extension.
		/// </summary>
		public VisualIconKind Icon
		{
			get => _icon;
			set => SetProperty(ref _icon, value);
		}

		private string? _tooltip;
		/// <summary>
		/// The tooltip for button associated with this extension.
		/// </summary>
		public string? Tooltip
		{
			get => _tooltip;
			set => SetProperty(ref _tooltip, value);
		}

		private ICommand? _command;
		/// <summary>
		/// The command for button associated with this extension.
		/// </summary>
		public ICommand? Command
		{
			get => _command;
			set => SetProperty(ref _command, value);
		}

		private bool _isButtonVisible = true;
		/// <summary>
		/// The visibility of the extension. If false, the extension will not be displayed.
		/// </summary>
		public bool IsButtonVisible
		{
			get => _isButtonVisible;
			set => SetProperty(ref _isButtonVisible, value);
		}

		private object? _additionalViewModel;
		/// <summary>
		/// The view model that will be appended to the end of the code block.
		/// </summary>
		public object? AdditionalViewModel
		{
			get => _additionalViewModel;
			set => SetProperty(ref _additionalViewModel, value);
		}
	}
}