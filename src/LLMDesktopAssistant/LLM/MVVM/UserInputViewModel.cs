using Avalonia.Collections;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using System.Collections.Specialized;
using System.ComponentModel;
using LLMDesktopAssistant.Controls.Dialogs;
using Material.Icons;
using LLMDesktopAssistant.LLM.MVVM.Attachments;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.MVVM.Additional;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.LLM.Settings;
using LLMDesktopAssistant.Settings;
using LLMDesktopAssistant.Tools.Consents;
using LLMDesktopAssistant.Users;
using LLMDesktopAssistant.Utils;
using Serilog;
using LLMDesktopAssistant.Localization;

namespace LLMDesktopAssistant.LLM.MVVM
{
	public class UserMessageVisibilityItemModel
	{
		public required MessageVisibility Visibility { get; init; }
		public required LocaleKeyBase Title { get; init; }
		public required MaterialIconKind Icon { get; init; }
	}

	[ViewModelFor(typeof(UserInputView))]
	public class UserInputViewModel : ViewModelBase
	{
		private class SendMessageCommandObject : ICommand
		{
			public event EventHandler? CanExecuteChanged;

			private readonly UserInputViewModel _vm;
			private readonly bool _generate;
			public SendMessageCommandObject(UserInputViewModel vm, bool generate)
			{
				_vm = vm;
				_generate = generate;
				_vm.Chat.SubscribeChanged(nameof(Chat.GenerationCts), _ =>
				{
					InvokeUI(() =>
					{
						CanExecuteChanged?.Invoke(this, EventArgs.Empty);
					});
				});
			}

			public bool CanExecute(object? parameter)
			{
				return _vm.Chat.GenerationCts == null;
			}

			public async void Execute(object? parameter)
			{
				try
				{
					await _vm.SendCurrentUserInputAsync(_generate);
				}
				catch (Exception ex)
				{
					Log.Error(ex, "Failed to send message: {Error}", ex.Message);
				}
			}
		}

		private class CancelEditCommandObject : ICommand
		{
			public event EventHandler? CanExecuteChanged;

			private readonly UserInputViewModel _vm;
			public CancelEditCommandObject(UserInputViewModel vm)
			{
				_vm = vm;
				_vm.SubscribeChanged(nameof(UserInputViewModel.EditingMessage), _ =>
				{
					InvokeUI(() =>
					{
						CanExecuteChanged?.Invoke(this, EventArgs.Empty);
					});
				});
			}

			public bool CanExecute(object? parameter)
			{
				return _vm.EditingMessage != null;
			}

			public void Execute(object? parameter)
			{
				try
				{
					_vm.EndEditing();
				}
				catch (Exception ex)
				{
					Log.Error(ex, "Failed to cancel edit: {Error}", ex.Message);
				}
			}
		}

		private class CancelGenerationCommandObject : ICommand
		{
			public event EventHandler? CanExecuteChanged;

			private readonly UserInputViewModel _vm;
			public CancelGenerationCommandObject(UserInputViewModel vm)
			{
				_vm = vm;
				_vm.Chat.SubscribeChanged(nameof(Chat.GenerationCts), _ =>
				{
					InvokeUI(() =>
					{
						CanExecuteChanged?.Invoke(this, EventArgs.Empty);
					});
				});
			}

			public bool CanExecute(object? parameter)
			{
				return _vm.Chat.GenerationCts != null;
			}

			public void Execute(object? parameter)
			{
				try
				{
					_vm.Chat.GenerationCts?.Cancel();
				}
				catch (Exception ex)
				{
					Log.Error(ex, "Failed to cancel generation: {Error}", ex.Message);
				}
			}
		}



		/// <summary>
		/// Gets the current chat instance.
		/// </summary>
		public Chat Chat { get; }

		private readonly IChatSettingsService _settingsService;

		private static readonly SettingsCategory<ChatSettings> chatSettingsCategory = SettingsManager.GetCategory<ChatSettings>();

		private SettingsIdItemViewModel _selectedSettingsId;
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
		private EventHandler? _usersSettingsChangedHandler;
		private IDisposable? _modelsSubscription;
		private ModelSelectionSettings? _trackedSelection;

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

		/// <summary>
		/// Subscribes to the local users of the current chat and refreshes
		/// <see cref="Users"/> when the settings or the user list change.
		/// </summary>
		private void TrackUsers()
		{
			_settingsService.Settings.Users.Users.CollectionChanged -= Users_CollectionChanged;
			_settingsService.Settings.Users.Users.CollectionChanged += Users_CollectionChanged;
			RefreshUsers();
		}

		private void Users_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
		{
			RefreshUsers();
		}

		private void RefreshUsers()
		{
			var currentLogin = SelectedUser?.Login;
			Users.Reset(_settingsService.Settings.Users.Users);

			SelectedUser = Users.FirstOrDefault(u => u.Login == currentLogin) ?? Users.FirstOrDefault();
			RaisePropertyChanged(nameof(HasMultipleUsers));
		}

		/// <summary>
		/// Gets the chat view model that holds this user input manager.
		/// </summary>
		public ChatViewModel ChatViewModel { get; }

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



		/// <summary>
		/// Command to send a message.
		/// </summary>
		public ICommand SendMessageCommand { get; }

		/// <summary>
		/// Command to send a message.
		/// </summary>
		public ICommand SendGenerateMessageCommand { get; }

		/// <summary>
		/// Command to cancel edit of the current message.
		/// </summary>
		public ICommand CancelEditCommand { get; }

		/// <summary>
		/// Command to cancel the current generation.
		/// </summary>
		public ICommand CancelGenerationCommand { get; }



		private string _text = string.Empty;
		private string _prevText = string.Empty;
		/// <summary>
		/// Gets or sets the user input to be sent in the next conversation turn.
		/// </summary>
		public string Text
		{
			get => _text;
			set => SetProperty(ref _text, value);
		}

		private readonly AvaloniaList<AttachmentViewModel> _attachments = [];
		private ImmutableList<AttachmentMessagePart> _prevAttachments = [];
		/// <summary>
		/// Gets or sets the attachments or additional buttons to be displayed with the current message.
		/// </summary>
		public ICollection<AttachmentViewModel> Attachments
		{
			get => _attachments;
			set
			{
				_attachments.Clear();
				_attachments.AddRange(value);
			}
		}

		private BranchedMessage? _editingMessage = null;
		/// <summary>
		/// Gets or sets the message that is currently being edited, if any.
		/// </summary>
		public BranchedMessage? EditingMessage
		{
			get => _editingMessage;
			private set => SetProperty(ref _editingMessage, value);
		}

		private bool _isGenerating = false;
		/// <summary>
		/// Gets or sets a value indicating whether the current message is being generated.
		/// </summary>
		public bool IsGenerating
		{
			get => _isGenerating;
			private set => SetProperty(ref _isGenerating, value);
		}

		private IDisposable? _generationCtsSubscription;

		public ImmutableList<UserMessageVisibilityItemModel> Visibilities { get; } = [
			new UserMessageVisibilityItemModel { Visibility = MessageVisibility.Always, Title = Locale.GetKey("message.visibility.always"), Icon = MaterialIconKind.Eye },
			new UserMessageVisibilityItemModel { Visibility = MessageVisibility.RevealAfterSend, Title = Locale.GetKey("message.visibility.reveal_after_send"), Icon = MaterialIconKind.Clock },
			new UserMessageVisibilityItemModel { Visibility = MessageVisibility.OnlyUsers, Title = Locale.GetKey("message.visibility.only_users"), Icon = MaterialIconKind.Account },
			new UserMessageVisibilityItemModel { Visibility = MessageVisibility.OnlyAgents, Title = Locale.GetKey("message.visibility.only_agents"), Icon = MaterialIconKind.Robot }
		];

		private UserMessageVisibilityItemModel _selectedVisibility;
		/// <summary>
		/// Gets or sets the visibility of the next user message.
		/// </summary>
		public UserMessageVisibilityItemModel SelectedVisibility
		{
			get => _selectedVisibility;
			set => SetProperty(ref _selectedVisibility, value);
		}

		/// <summary>
		/// Gets the list of local users that can send messages in this chat.
		/// </summary>
		public RangeObservableCollection<UserInformation> Users { get; } = [];

		private UserInformation? _selectedUser;
		/// <summary>
		/// Gets or sets the user that sends the next message.
		/// </summary>
		public UserInformation? SelectedUser
		{
			get => _selectedUser;
			set => SetProperty(ref _selectedUser, value);
		}

		/// <summary>
		/// Gets a value indicating whether more than one local user is available.
		/// </summary>
		public bool HasMultipleUsers => Users.Count > 1;

		public UserInputViewModel(ChatViewModel chatVM)
		{
			Chat = chatVM.Chat;
			ChatViewModel = chatVM;
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

			_usersSettingsChangedHandler = (_, _) => TrackUsers();
			_settingsService.SettingsChanged += _usersSettingsChangedHandler;
			TrackUsers();

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
				var viewModel = new AttachmentsManagerViewModel(this);
				await DialogManager.ShowDialogAsync(viewModel);
			});

			OpenBlazorWebUICommand = new AsyncRelayCommand(async () =>
			{
				var viewModel = new BlazorHostViewModel(Chat.Services);
				await DialogManager.ShowDialogAsync(viewModel);
			});



			SendMessageCommand = new SendMessageCommandObject(this, generate: false);
			SendGenerateMessageCommand = new SendMessageCommandObject(this, generate: true);
			CancelEditCommand = new CancelEditCommandObject(this);
			CancelGenerationCommand = new CancelGenerationCommandObject(this);

			IsGenerating = Chat.GenerationCts != null;
			Chat.SubscribeChanged(nameof(Chat.GenerationCts), _ =>
			{
				InvokeUI(() =>
				{
					IsGenerating = Chat.GenerationCts != null;
				});
			}, out _generationCtsSubscription);

			_selectedVisibility = Visibilities[0];
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

		public UserInput? GetCurrentUserInput()
		{
			if (IsEmpty)
				return null;

			var userManager = Chat.Services.GetRequiredService<IUserManagementService>();

			return new UserInput
			{
				Content = _text,
				SenderLogin = SelectedUser?.Login ?? userManager.GetLocalUsers().FirstOrDefault()?.Login ?? "user",
				Parts = _attachments.Select(a => (AdditionalChatData)a.Attachment).ToImmutableList(),
				Visibility = _selectedVisibility.Visibility,
			};
		}

		public void EditMessage(BranchedMessage branchedMessage)
		{
			if (branchedMessage.Message is not UserMessage userMessage)
				throw new ArgumentException("The branched message does not contain a user message.");

			if (EditingMessage != null)
			{
				_prevText = _text;
				_prevAttachments = _attachments.Select(am => am.Attachment).ToImmutableList();
			}
			EditingMessage = branchedMessage;
			Text = userMessage.Content;
			Attachments = userMessage.AdditionalData.GetAll<AttachmentMessagePart>()
				.Select(a => new AttachmentViewModel(this, a)).ToList();
		}

		public void Clear()
		{
			Text = string.Empty;
			Attachments = [];
			EditingMessage = null;
		}

		public void EndEditing()
		{
			Text = _prevText;
			Attachments = _prevAttachments.Select(a => new AttachmentViewModel(this, a)).ToList();
			_prevText = string.Empty;
			_prevAttachments = [];
			EditingMessage = null;
		}

		public bool IsEmpty => string.IsNullOrWhiteSpace(_text) && Attachments.Count == 0;

		public async Task AcceptDropAsync(DragEventArgs args)
		{
			var viewModel = new AttachmentsManagerViewModel(this);
			viewModel.AcceptDrop(args);
			await DialogManager.ShowDialogAsync(viewModel);
		}

		public async Task AcceptImageAsync(Bitmap image)
		{
			var viewModel = new AttachmentsManagerViewModel(this);
			viewModel.AcceptImage(image);
			await DialogManager.ShowDialogAsync(viewModel);
		}

		public async Task AcceptFilesAsync(IStorageItem[] files)
		{
			var viewModel = new AttachmentsManagerViewModel(this);
			viewModel.AcceptFiles(files);
			await DialogManager.ShowDialogAsync(viewModel);
		}



		/// <summary>
		/// Sends a message to the LLM and updates the conversation turns.
		/// </summary>
		/// <param name="cts">The cancellation token to monitor for cancellation requests.</param>
		/// <returns>A task that represents the asynchronous operation.</returns>
		public Task SendCurrentUserInputAsync(bool generate, CancellationToken cts = default)
		{
			var userInput = GetCurrentUserInput();
			var editingMessage = EditingMessage;

			EndEditing();
			if (userInput != null)
			{
				var chatOperator = Chat.Services.GetRequiredService<IChatOperationService>();
				if (editingMessage != null)
					return chatOperator.SendEditedUserInputAsync(editingMessage.MessageIndex, userInput, generate, cts);
				return chatOperator.SendUserInputAsync(userInput, generate, cts);
			}

			return Task.CompletedTask;
		}

		/// <inheritdoc/>
		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (disposing)
			{
				SettingsManager.GetCategory<ChatSettings>().Ids.CollectionChanged -= SettingsIds_CollectionChanged;

				if (_settingsChangedHandler is not null)
					_settingsService.SettingsChanged -= _settingsChangedHandler;
				if (_usersSettingsChangedHandler is not null)
					_settingsService.SettingsChanged -= _usersSettingsChangedHandler;
				_settingsChangedHandler = null;
				_usersSettingsChangedHandler = null;

				_modelsSubscription?.Dispose();
				_modelsSubscription = null;
				_settingsService.Settings.Users.Users.CollectionChanged -= Users_CollectionChanged;

				_settingsService.Settings.Models.PropertyChanged -= Models_PropertyChanged;
				if (_trackedSelection is not null)
					_trackedSelection.PropertyChanged -= EffectiveSelection_PropertyChanged;
				_trackedSelection = null;

				_generationCtsSubscription?.Dispose();
				_generationCtsSubscription = null;
			}
		}
	}
}