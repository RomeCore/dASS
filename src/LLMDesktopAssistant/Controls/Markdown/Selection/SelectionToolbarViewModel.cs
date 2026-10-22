using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using LLMDesktopAssistant.Controls.Icons;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.MVVM;
using LLMDesktopAssistant.Utils;
using Material.Icons;

namespace LLMDesktopAssistant.Controls.Markdown.Selection
{
	/// <summary>
	/// One button of the selection toolbar: what it says, what it shows and which processor it runs.
	/// </summary>
	public sealed class SelectionToolbarItemViewModel : NotifyPropertyChanged
	{
		/// <summary>
		/// Gets the processor this button stands for.
		/// </summary>
		public required IMarkdownSelectionProcessor Processor { get; init; }

		/// <summary>
		/// Gets the command that runs <see cref="Processor"/> against the snapshot the toolbar was built for.
		/// </summary>
		public required ICommand Command { get; init; }

		/// <summary>
		/// Gets the localized button title.
		/// </summary>
		public LocaleKeyBase Title => Processor.Title;

		/// <summary>
		/// Gets the button icon.
		/// </summary>
		public VisualIconKind Icon => Processor.Icon;
	}

	/// <summary>
	/// The content of the selection toolbar: the buttons for one confirmed selection.
	/// </summary>
	/// <remarks>
	/// The view model owns the button list and the command each button runs; the toolbar control owns the popup,
	/// the selection and the error reporting, and hands its command in once, when it is constructed.
	/// </remarks>
	public sealed class SelectionToolbarViewModel : ViewModelBase
	{
		/// <summary>
		/// Gets the buttons, already filtered by applicability and sorted by order.
		/// </summary>
		public RangeObservableCollection<SelectionToolbarItemViewModel> Items { get; } = [];

		private IAsyncRelayCommand<IMarkdownSelectionProcessor>? _processCommand;

		/// <summary>
		/// Gets or sets the command a button runs; its parameter is the button's
		/// <see cref="SelectionToolbarItemViewModel.Processor"/>.
		/// </summary>
		public IAsyncRelayCommand<IMarkdownSelectionProcessor>? ProcessCommand
		{
			get => _processCommand;
			set
			{
				if (SetProperty(ref _processCommand, value))
					RaisePropertyChanged(nameof(CanProcess));
			}
		}

		/// <summary>
		/// Gets a value indicating whether there is a command to run.
		/// </summary>
		public bool CanProcess => _processCommand is not null;

		/// <summary>
		/// Rebuilds the buttons for a selection.
		/// </summary>
		/// <returns>
		/// <see langword="true"/> when there is at least one applicable processor, so the toolbar has something to
		/// show; <see langword="false"/> otherwise.
		/// </returns>
		public bool Populate(MarkdownSelectionContext context, IMarkdownSelectionProcessorsProvider? provider)
		{
			Items.Clear();

			if (provider is null || _processCommand is null)
				return false;

			foreach (var processor in provider.GetProcessors(context)
				.Where(static processor => processor is not null)
				.Where(processor => processor.IsApplicable(context))
				.OrderBy(static processor => processor.Order))
			{
				Items.Add(new SelectionToolbarItemViewModel
				{
					Processor = processor,
					Command = _processCommand,
				});
			}

			return Items.Count > 0;
		}

		/// <summary>
		/// Clears the buttons (the toolbar is closing).
		/// </summary>
		public void Clear() => Items.Clear();
	}
}
