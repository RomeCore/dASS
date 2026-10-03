using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LLMDesktopAssistant.Controls.Dialogs;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.MVVM.Attachments;
using LLMDesktopAssistant.LLM.MVVM.Settings;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.LLM.Settings;
using LLMDesktopAssistant.Settings;
using LLMDesktopAssistant.Tools.Consents;
using LLMDesktopAssistant.Users;
using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.LLM.MVVM
{
	/// <summary>
	/// The left chat toolbar: configuration and tooling buttons (settings profile, model selector,
	/// memorized consents, attachments manager and the Blazor Web UI host).
	/// </summary>
	[ViewModelFor(typeof(ChatLeftToolBarView))]
	public class ChatLeftToolBarViewModel : ViewModelBase
	{
		private static readonly SettingsCategory<ChatSettings> chatSettingsCategory = SettingsManager.GetCategory<ChatSettings>();

		private readonly UserInputViewModel _userInput;
		private readonly IChatSettingsService _settingsService;

		/// <summary>
		/// Gets the chat this toolbar belongs to.
		/// </summary>
		public Chat Chat => _userInput.Chat;

		private SettingsIdItemViewModel _selectedSettingsId;
		/// <summary>
		/// Gets or sets the selected settings profile of the chat.
		/// </summary>
		public SettingsIdItemViewModel SelectedSettingsId
		{
			get => _selectedSettingsId;
			set
			{
				if (SetProperty(ref _selectedSettingsId, value))
					_settingsService.SetSettings(chatSettingsCategory.Get(value.Id));
			}
		}

		/// <summary>
		/// Gets a list of settings IDs for the current chat.
		/// </summary>
		public RangeObservableCollection<SettingsIdItemViewModel> SettingsIds { get; }

		/// <summary>
		/// Gets or sets the chat model selected in the input bar.
		/// The value is routed through the effective (inherited) model selection of the chat.
		/// </summary>
		public string ChatModel
		{
			get => _settingsService.Settings.Models.GetEffectiveSelection().ChatModel;
			set
			{
				var selection = _settingsService.Settings.Models.GetEffectiveSelection();
				if (selection.ChatModel != value)
					selection.ChatModel = value;
			}
		}

		private EventHandler? _settingsChangedHandler;
		private IDisposable? _modelsSubscription;
		private ModelSelectionSettings? _trackedSelection;

		/// <summary>
		/// Command to open settings.
		/// </summary>
		public ICommand OpenSettingsCommand { get; }

		/// <summary>
		/// Command to open the consent memorization manager dialog.
		/// </summary>
		public ICommand OpenConsentMemorizationCommand { get; }

		/// <summary>
		/// Command to open attachments manager.
		/// </summary>
		public ICommand OpenAttachmentsManagerCommand { get; }

		/// <summary>
		/// Command to open Blazor Web UI hosting dialog.
		/// </summary>
		public ICommand OpenBlazorWebUICommand { get; }

		public ChatLeftToolBarViewModel(UserInputViewModel userInput)
		{
			_userInput = userInput;
			_settingsService = Chat.Services.GetRequiredService<IChatSettingsService>();

			chatSettingsCategory.Ids.CollectionChanged += SettingsIds_CollectionChanged;
			SettingsIds = [ .. chatSettingsCategory.Ids
				.Where(c => c != SettingsObject.DefaultId)
				.Select(c => new SettingsIdItemViewModel { Id = c })
				.Prepend(SettingsIdItemViewModel.Default) ];
			_selectedSettingsId = SettingsIds.First(id => id.Id == _settingsService.Settings.Id);

			_settingsChangedHandler = (_, _) =>
			{
				TrackModelSelection();
				SelectedSettingsId = SettingsIds.First(id => id.Id == _settingsService.Settings.Id);
			};
			_settingsService.SettingsChanged += _settingsChangedHandler;
			TrackModelSelection();

			OpenSettingsCommand = new AsyncRelayCommand(async () =>
			{
				var viewModel = new SettingsCategoryViewModel<ChatSettings>(cs => new ChatSettingsViewModel(cs, Chat),
					true, newSettings => _settingsService.SetSettings(newSettings), _settingsService.Settings.Id);
				try
				{
					await DialogManager.ShowDialogAsync(viewModel);
				}
				finally
				{
					viewModel.Dispose();
				}
			});

			OpenConsentMemorizationCommand = new AsyncRelayCommand(async () =>
			{
				var viewModel = new ConsentMemorizationViewModel(Chat);
				await DialogManager.ShowDialogAsync(viewModel);
			});

			OpenAttachmentsManagerCommand = new AsyncRelayCommand(async () =>
			{
				var viewModel = new AttachmentsManagerViewModel(_userInput);
				await DialogManager.ShowDialogAsync(viewModel);
			});

			OpenBlazorWebUICommand = new AsyncRelayCommand(async () =>
			{
				var viewModel = new BlazorHostViewModel(Chat.Services);
				await DialogManager.ShowDialogAsync(viewModel);
			});
		}

		/// <summary>
		/// Subscribes to the model settings of the current chat and raises
		/// <see cref="ChatModel"/> change notifications when the effective value changes.
		/// </summary>
		private void TrackModelSelection()
		{
			_modelsSubscription?.Dispose();
			_settingsService.Settings.SubscribeChanged(nameof(ChatSettings.Models), _ =>
			{
				_settingsService.Settings.Models.PropertyChanged -= Models_PropertyChanged;
				_settingsService.Settings.Models.PropertyChanged += Models_PropertyChanged;
				TrackEffectiveSelection();
				RaisePropertyChanged(nameof(ChatModel));
			}, out _modelsSubscription);

			_settingsService.Settings.Models.PropertyChanged -= Models_PropertyChanged;
			_settingsService.Settings.Models.PropertyChanged += Models_PropertyChanged;
			TrackEffectiveSelection();
			RaisePropertyChanged(nameof(ChatModel));
		}

		private void Models_PropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName != nameof(ChatSettings.Models.Selection))
				return;

			TrackEffectiveSelection();
			RaisePropertyChanged(nameof(ChatModel));
		}

		/// <summary>
		/// Tracks the currently effective model selection object so that changes of its
		/// <see cref="ModelSelectionSettings.ChatModel"/> are reflected in the input bar.
		/// </summary>
		private void TrackEffectiveSelection()
		{
			var selection = _settingsService.Settings.Models.GetEffectiveSelection();
			if (ReferenceEquals(_trackedSelection, selection))
				return;

			if (_trackedSelection is not null)
				_trackedSelection.PropertyChanged -= EffectiveSelection_PropertyChanged;
			_trackedSelection = selection;
			_trackedSelection.PropertyChanged += EffectiveSelection_PropertyChanged;
		}

		private void EffectiveSelection_PropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(ModelSelectionSettings.ChatModel))
				RaisePropertyChanged(nameof(ChatModel));
		}

		private void SettingsIds_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
		{
			if (e.OldItems != null)
				foreach (string id in e.OldItems)
					SettingsIds.Remove(SettingsIds.First(s => s.Id == id));

			if (e.NewItems != null)
				foreach (string id in e.NewItems)
					SettingsIds.Add(new SettingsIdItemViewModel { Id = id });
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (disposing)
			{
				SettingsManager.GetCategory<ChatSettings>().Ids.CollectionChanged -= SettingsIds_CollectionChanged;

				if (_settingsChangedHandler is not null)
					_settingsService.SettingsChanged -= _settingsChangedHandler;
				_settingsChangedHandler = null;

				_modelsSubscription?.Dispose();
				_modelsSubscription = null;

				_settingsService.Settings.Models.PropertyChanged -= Models_PropertyChanged;
				if (_trackedSelection is not null)
					_trackedSelection.PropertyChanged -= EffectiveSelection_PropertyChanged;
				_trackedSelection = null;
			}
		}
	}
}
