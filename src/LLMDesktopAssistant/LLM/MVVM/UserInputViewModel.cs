using System.ComponentModel;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using LLMDesktopAssistant.Controls.Dialogs;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.MVVM.Additional;
using LLMDesktopAssistant.LLM.MVVM.Attachments;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Utils;
using Material.Icons;
using Serilog;

namespace LLMDesktopAssistant.LLM.MVVM
{
	public class UserMessageVisibilityItemModel
	{
		public required MessageVisibility Visibility { get; init; }
		public required LocaleKeyBase Title { get; init; }
		public required VisualIconKind Icon { get; init; }
	}

	/// <summary>
	/// The chat user input.
	/// <para/>
	/// The persisted <see cref="UserInputState"/> always holds the draft for a NEW message
	/// (text + parts). Entering edit mode displaces this draft from the <em>view</em> only:
	/// the edited message is loaded into an in-memory buffer, and the draft stays untouched
	/// in <see cref="UserInputState"/> (and thus in the database), ready to be restored.
	/// </summary>
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

		/// <summary>
		/// Gets the chat view model that holds this user input.
		/// </summary>
		public ChatViewModel ChatViewModel { get; }

		/// <summary>
		/// Gets the persisted user input state (the NEW-message draft: text, parts, sender and visibility).
		/// </summary>
		public UserInputState UserInputState => Chat.UserInputState;

		// The draft parts are owned by the persisted UserInputState; the edit parts live only in memory.
		private readonly AdditionalChatDataCollectionViewModel _draftData;
		private readonly AdditionalChatDataCollection _editParts = new();
		private readonly AdditionalChatDataCollectionViewModel _editData;

		/// <summary>
		/// Gets the message parts currently shown in the input: the edited message parts while editing,
		/// otherwise the persisted draft parts. Always in editing mode (with remove overlays).
		/// </summary>
		public AdditionalChatDataCollectionViewModel AdditionalData => EditingMessage != null ? _editData : _draftData;

		private BranchedMessage? _editingMessage = null;
		/// <summary>
		/// Gets or sets the message that is currently being edited, if any.
		/// </summary>
		public BranchedMessage? EditingMessage
		{
			get => _editingMessage;
			private set
			{
				if (SetProperty(ref _editingMessage, value))
				{
					RaisePropertyChanged(nameof(AdditionalData));
					RaisePropertyChanged(nameof(Text));
				}
			}
		}

		private string _editText = string.Empty;

		/// <summary>
		/// Gets or sets the text shown in the input.
		/// While editing it is the edited message content (in memory); otherwise it is the persisted draft text.
		/// </summary>
		public string Text
		{
			get => EditingMessage != null ? _editText : UserInputState.Text;
			set
			{
				if (EditingMessage != null)
				{
					if (_editText != value)
					{
						_editText = value;
						RaisePropertyChanged(nameof(Text));
					}
				}
				else if (UserInputState.Text != value)
					UserInputState.Text = value;
			}
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

		/// <summary>
		/// Command to send a message (without generation).
		/// </summary>
		public ICommand SendMessageCommand { get; }

		/// <summary>
		/// Command to send a message and start generation.
		/// </summary>
		public ICommand SendGenerateMessageCommand { get; }

		/// <summary>
		/// Command to cancel editing of the current message.
		/// </summary>
		public ICommand CancelEditCommand { get; }

		/// <summary>
		/// Command to cancel the current generation.
		/// </summary>
		public ICommand CancelGenerationCommand { get; }

		public UserInputViewModel(ChatViewModel chatVM)
		{
			Chat = chatVM.Chat;
			ChatViewModel = chatVM;

			_draftData = new AdditionalChatDataCollectionViewModel(Chat.UserInputState.Parts);
			_draftData.Parts.IsEditing = true;

			_editData = new AdditionalChatDataCollectionViewModel(_editParts);
			_editData.Parts.IsEditing = true;

			UserInputState.PropertyChanged += OnUserInputStatePropertyChanged;

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
		}

		private void OnUserInputStatePropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(UserInputState.Text) && EditingMessage == null)
				RaisePropertyChanged(nameof(Text));
		}

		/// <summary>
		/// Gets a value indicating whether there is nothing to send.
		/// </summary>
		public bool IsEmpty =>
			EditingMessage != null
				? string.IsNullOrWhiteSpace(_editText) && _editParts.Count == 0
				: UserInputState.IsEmpty;

		public UserInput? GetCurrentUserInput()
		{
			if (EditingMessage != null)
			{
				if (string.IsNullOrWhiteSpace(_editText) && _editParts.Count == 0)
					return null;

				return new UserInput
				{
					Content = _editText,
					SenderLogin = UserInputState.SenderLogin,
					Parts = [.. _editParts],
					Visibility = UserInputState.Visibility,
				};
			}

			if (UserInputState.IsEmpty)
				return null;

			return new UserInput
			{
				Content = UserInputState.Text,
				SenderLogin = UserInputState.SenderLogin,
				Parts = [.. UserInputState.Parts],
				Visibility = UserInputState.Visibility,
			};
		}

		/// <summary>
		/// Loads a message into the edit buffer. The persisted draft (<see cref="UserInputState"/>)
		/// is not modified and remains available in the database.
		/// </summary>
		public void EditMessage(BranchedMessage branchedMessage)
		{
			if (branchedMessage.Message is not UserMessage userMessage)
				throw new ArgumentException("The branched message does not contain a user message.");

			_editParts.Clear();
			foreach (var part in userMessage.AdditionalData.OfType<AttachmentMessagePart>())
				_editParts.Add(part.Clone());

			_editText = userMessage.Content ?? string.Empty;
			EditingMessage = branchedMessage;
		}

		public void Clear()
		{
			EndEditing();
			UserInputState.Text = string.Empty;
			UserInputState.Parts.Clear();
		}

		/// <summary>
		/// Adds a message part to the input: to the edit buffer while editing, otherwise to the persisted draft.
		/// </summary>
		public void AddPart(AdditionalChatData part)
		{
			if (EditingMessage != null)
				_editParts.Add(part);
			else
				UserInputState.Parts.Add(part);
		}

		/// <summary>
		/// Discards the edit buffer and returns to the persisted draft.
		/// </summary>
		public void EndEditing()
		{
			if (EditingMessage == null && _editParts.Count == 0 && string.IsNullOrEmpty(_editText))
				return;

			EditingMessage = null;
			_editText = string.Empty;
			_editParts.Clear();
		}

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
		/// <param name="generate">Whether to start generation after sending.</param>
		/// <param name="cts">The cancellation token to monitor for cancellation requests.</param>
		public Task SendCurrentUserInputAsync(bool generate, CancellationToken cts = default)
		{
			var editingMessage = EditingMessage;
			var userInput = GetCurrentUserInput();

			if (editingMessage != null)
			{
				// Editing: drop the in-memory edit buffer, the persisted draft was never touched.
				EndEditing();
			}
			else
			{
				// Sending a new message: clear the just-sent draft.
				UserInputState.Text = string.Empty;
				UserInputState.Parts.Clear();
			}

			if (userInput != null)
			{
				var chatOperator = Chat.Services.GetRequiredService<IChatOperationService>();
				if (editingMessage != null)
					return chatOperator.SendEditedUserInputAsync(editingMessage.MessageIndex, userInput, generate, cts);
				return chatOperator.SendUserInputAsync(userInput, generate, cts);
			}

			return Task.CompletedTask;
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (disposing)
			{
				UserInputState.PropertyChanged -= OnUserInputStatePropertyChanged;

				_generationCtsSubscription?.Dispose();
				_generationCtsSubscription = null;

				_draftData.Dispose();
				_editData.Dispose();
				_editParts.Dispose();
			}
		}
	}
}
