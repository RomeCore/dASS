using Material.Icons;

namespace LLMDesktopAssistant.UIExtensions.MessageExtensions
{
	/// <summary>
	/// Represents an extension for a message, which can be used to add additional functionality or UI elements to messages.
	/// </summary>
	public abstract class MessageExtension : NotifyPropertyChanged
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

		private bool _isVisible = true;
		/// <summary>
		/// The visibility of the extension. If false, the extension will not be displayed.
		/// </summary>
		public bool IsVisible
		{
			get => _isVisible;
			set => SetProperty(ref _isVisible, value);
		}

		private object? _viewModel;
		/// <summary>
		/// The view model that will be shown instead of button if not null.
		/// </summary>
		public object? ViewModel
		{
			get => _viewModel;
			set => SetProperty(ref _viewModel, value);
		}
	}
}
