using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Controls.Dialogs;
using LLMDesktopAssistant.Controls.Text;
using LLMDesktopAssistant.InputCompletion;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.MVVM.Additional;
using LLMDesktopAssistant.LLM.MVVM.Attachments;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Services.Instances;
using LLMDesktopAssistant.SlashCommands;
using LLMDesktopAssistant.SlashCommands.Input;
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
				_vm._executionTokens.ExecutionCancellationTokenChanged += OnExecutionStateChanged;
			}

			private void OnExecutionStateChanged()
			{
				InvokeUI(() => CanExecuteChanged?.Invoke(this, EventArgs.Empty));
			}

			public bool CanExecute(object? parameter)
			{
				// The box stays usable while a run is going: the matrix decides between queueing and stopping.
				return true;
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
				_vm._executionTokens.ExecutionCancellationTokenChanged += OnExecutionStateChanged;
			}

			private void OnExecutionStateChanged()
			{
				InvokeUI(() => CanExecuteChanged?.Invoke(this, EventArgs.Empty));
			}

			public bool CanExecute(object? parameter)
			{
				return _vm._executionTokens.ExecutionCancellationToken != null;
			}

			public void Execute(object? parameter)
			{
				try
				{
					// The Stop button is a hard stop: it never injects the queue.
					_ = _vm.StopGenerationAsync();
				}
				catch (Exception ex)
				{
					Log.Error(ex, "Failed to cancel generation: {Error}", ex.Message);
				}
			}
		}

		// The draft parts are owned by the persisted UserInputState; the edit parts live only in memory.
		private readonly AdditionalChatDataCollectionViewModel _draftData;
		private readonly AdditionalChatDataCollectionViewModel _editData;

		/// <summary>
		/// Gets the current chat instance.
		/// </summary>
		public Chat Chat { get; }

		/// <summary>
		/// Gets the chat view model that holds this user input.
		/// </summary>
		public ChatViewModel ChatViewModel { get; }

		/// <summary>
		/// Gets the editing user input state.
		/// </summary>
		public UserInputState EditUserInputState { get; } = new();

		/// <summary>
		/// Gets the persisted user input state (the NEW-message draft: text, parts, sender and visibility).
		/// </summary>
		public UserInputState UserInputState => Chat.UserInputState;

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
					RaiseInputStateDependentProperties();
				}
			}
		}

		/// <summary>
		/// Gets or sets the text shown in the input.
		/// While editing it is the edited message content (in memory); otherwise it is the persisted draft text.
		/// </summary>
		public string Text
		{
			get => EditingMessage != null ? EditUserInputState.Content : UserInputState.Content;
			set
			{
				if (EditingMessage != null)
				{
					if (EditUserInputState.Content != value)
					{
						EditUserInputState.Content = value;
						RaiseInputStateDependentProperties();
						CompletionViewModel.Update(value, _completionCaretIndex);
					}
				}
				else if (UserInputState.Content != value)
				{
					UserInputState.Content = value;
					RaiseInputStateDependentProperties();
					CompletionViewModel.Update(value, _completionCaretIndex);
				}
			}
		}

		private bool _isGenerating = false;
		/// <summary>
		/// Gets or sets a value indicating whether the current message is being generated.
		/// </summary>
		public bool IsGenerating
		{
			get => _isGenerating;
			private set
			{
				if (SetProperty(ref _isGenerating, value))
					RaiseInputStateDependentProperties();
			}
		}

		private readonly IChatExecutionTokenService _executionTokens;
		private readonly IFollowUpChatMessageQueue _followUps;

		/// <summary>
		/// Gets a value indicating whether the chat holds pending follow-ups.
		/// </summary>
		public bool HasFollowUps => _followUps.HasAny;

		/// <summary>
		/// Gets or sets the level the next queued message is composed with. Persisted with the queue itself, so the last
		/// picked value survives a restart.
		/// </summary>
		public FollowUpInjectionLevel SelectedLevel
		{
			get => _followUps.DefaultLevel;
			set
			{
				if (_followUps.DefaultLevel == value)
					return;

				_followUps.DefaultLevel = value;
				RaisePropertyChanged(nameof(SelectedLevel));
			}
		}

		private int _completionCaretIndex;

		/// <summary>
		/// The input completion state (popup + selection) for the current text and caret.
		/// </summary>
		public InputCompletionViewModel CompletionViewModel { get; }

		/// <summary>
		/// The renderer for the input: the slash-command palette and the ghost preview.
		/// </summary>
		public IHighlightTransformProvider CompletionTransformProvider { get; }

		/// <summary>
		/// Recomputes the completion for the caret the input control reported.
		/// </summary>
		public void OnCompletionCaretChanged(int caretIndex)
		{
			_completionCaretIndex = caretIndex;
			CompletionViewModel.Update(Text, caretIndex);
		}

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

		/// <summary>
		/// Gets the chips shown above the input: the pending follow-up queue and the message being edited.
		/// </summary>
		public UserInputChipsViewModel Chips { get; }

		public UserInputViewModel(ChatViewModel chatVM)
		{
			Chat = chatVM.Chat;
			ChatViewModel = chatVM;
			_executionTokens = Chat.Services.GetRequiredService<IChatExecutionTokenService>();
			_followUps = Chat.Services.GetRequiredService<IFollowUpChatMessageQueue>();

			var inputCompletionService = Chat.Services.GetRequiredService<IInputCompletionService>();
			CompletionViewModel = new InputCompletionViewModel(inputCompletionService);
			CompletionTransformProvider = inputCompletionService.CompletionTransformProvider;

			_editData = new AdditionalChatDataCollectionViewModel(EditUserInputState.Parts);
			_editData.Parts.IsEditing = true;

			_draftData = new AdditionalChatDataCollectionViewModel(UserInputState.Parts);
			_draftData.Parts.IsEditing = true;

			EditUserInputState.PropertyChanged += OnUserInputStatePropertyChanged;
			UserInputState.PropertyChanged += OnUserInputStatePropertyChanged;

			SendMessageCommand = new SendMessageCommandObject(this, generate: false);
			SendGenerateMessageCommand = new SendMessageCommandObject(this, generate: true);
			CancelEditCommand = new CancelEditCommandObject(this);
			CancelGenerationCommand = new CancelGenerationCommandObject(this);

			IsGenerating = _executionTokens.ExecutionCancellationToken != null;
			_executionTokens.ExecutionCancellationTokenChanged += OnExecutionStateChanged;

			// The placeholder names what Enter will do, so it has to follow both the history and the queue.
			Chat.Messages.CollectionChanged += OnChatMessagesChanged;
			_followUps.Changed += OnFollowUpsChanged;

			Chips = new UserInputChipsViewModel(this);
		}

		private void OnExecutionStateChanged()
		{
			InvokeUI(() => IsGenerating = _executionTokens.ExecutionCancellationToken != null);
		}

		private void OnUserInputStatePropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(UserInputState.Content))
				RaisePropertyChanged(nameof(Text));
		}

		private void OnChatMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
			=> InvokeUI(RaiseInputStateDependentProperties);

		private void OnFollowUpsChanged() => InvokeUI(RaiseInputStateDependentProperties);

		/// <summary>
		/// Raises the properties that read the whole input state: the placeholder text and whether the last turn can
		/// be repeated.
		/// </summary>
		private void RaiseInputStateDependentProperties()
		{
			RaisePropertyChanged(nameof(PlaceholderKey));
			RaisePropertyChanged(nameof(CanResendTail));
		}

		/// <summary>
		/// Gets a value indicating whether there is nothing to send.
		/// </summary>
		public bool IsEmpty =>
			EditingMessage != null
				? EditUserInputState.IsEmpty
				: UserInputState.IsEmpty;

		/// <summary>
		/// Gets a value indicating whether the history's tail is a user message with no answer - the state in which
		/// Enter repeats that turn instead of sending an empty message.
		/// </summary>
		public bool CanResendTail =>
			!IsGenerating && IsEmpty && !HasFollowUps &&
			Chat.Messages.Count > 0 && Chat.Messages[Chat.Messages.Count - 1].Message is UserMessage;

		/// <summary>
		/// The placeholder of the input: it names what Enter will do in the current state, so the key matrix is visible
		/// before the key is pressed.
		/// </summary>
		public LocaleKeyBase PlaceholderKey
		{
			get
			{
				if (EditingMessage != null)
					return Locale.GetKey(IsEmpty ? "chat.input.hint.edit_empty" : "chat.input.hint.edit");

				if (IsGenerating)
				{
					if (!IsEmpty)
						return Locale.GetKey("chat.input.hint.queue");

					return Locale.GetKey(HasFollowUps ? "chat.input.hint.stop_flush" : "chat.input.hint.stop");
				}

				if (!IsEmpty)
					return Locale.GetKey("chat.input.hint.send");
				if (HasFollowUps)
					return Locale.GetKey("chat.input.hint.flush");
				if (CanResendTail)
					return Locale.GetKey("chat.input.hint.answer_last");
				if (Chat.Messages.Count == 0)
					return Locale.GetKey("chat.input.hint.first");

				return Locale.GetKey("chat.input.hint");
			}
		}

		public UserInputState? GetCurrentUserInput()
		{
			if (EditingMessage != null)
			{
				if (EditUserInputState.IsEmpty)
					return null;

				return EditUserInputState.Clone();
			}

			if (UserInputState.IsEmpty)
				return null;

			return UserInputState.Clone();
		}

		/// <summary>
		/// Loads a message into the edit buffer. The persisted draft (<see cref="UserInputState"/>)
		/// is not modified and remains available in the database.
		/// </summary>
		public void EditMessage(BranchedMessage branchedMessage)
		{
			if (branchedMessage.Message is not UserMessage userMessage)
				throw new ArgumentException("The branched message does not contain a user message.");

			EditUserInputState.FillFromMessage(userMessage);
			EditingMessage = branchedMessage;
		}

		public void Clear()
		{
			EndEditing();
			UserInputState.Clear();
			CompletionViewModel.Close();
		}

		/// <summary>
		/// Adds a message part to the input: to the edit buffer while editing, otherwise to the persisted draft.
		/// </summary>
		public void AddPart(AdditionalChatData part)
		{
			if (EditingMessage != null)
				EditUserInputState.Parts.Add(part);
			else
				UserInputState.Parts.Add(part);
		}

		/// <summary>
		/// Discards the edit buffer and returns to the persisted draft.
		/// </summary>
		public void EndEditing()
		{
			if (EditingMessage == null && EditUserInputState.IsEmpty)
				return;

			EditingMessage = null;
			EditUserInputState.Clear();
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

			if (userInput is null)
				return Task.CompletedTask;

			// Pre-flight: an unknown command or an invalid argument list refuses the send before the draft is cleared,
			// so the user's text survives a refusal.
			var insertion = Chat.Services.GetRequiredService<IChatMessageInsertionService>();
			var check = insertion.CanInsertUserInput(userInput);
			if (!check.Success)
			{
				Chat.Services.GetRequiredService<IToastService>().ShowError(check.Error!.Value);
				return Task.CompletedTask;
			}

			if (editingMessage != null)
			{
				// Editing: drop the in-memory edit buffer, the persisted draft was never touched.
				EndEditing();
			}
			else
			{
				// Sending a new message: clear the just-sent draft.
				UserInputState.Content = string.Empty;
				UserInputState.Parts.Clear();
			}

			var chatOperator = Chat.Services.GetRequiredService<IChatOperationService>();
			if (editingMessage != null)
				return chatOperator.SendEditedUserInputAsync(editingMessage.MessageIndex, userInput, generate, cts);
			return chatOperator.SendUserInputAsync(userInput, generate, cts);
		}

		/// <summary>
		/// Puts the current draft into the follow-up queue instead of sending it, for the case where a run is going.
		/// </summary>
		/// <param name="generate">Whether the message asks for an answer once it is injected.</param>
		public async Task EnqueueCurrentUserInputAsync(bool generate)
		{
			var userInput = GetCurrentUserInput();
			if (userInput is null)
				return;

			// Same pre-flight as a send: a refused command keeps the text where the user can see it.
			var insertion = Chat.Services.GetRequiredService<IChatMessageInsertionService>();
			var check = insertion.CanInsertUserInput(userInput);
			if (!check.Success)
			{
				Chat.Services.GetRequiredService<IToastService>().ShowError(check.Error!.Value);
				return;
			}

			var chatOperator = Chat.Services.GetRequiredService<IChatOperationService>();
			var queued = chatOperator.QueueUserInputAsync(userInput, generate);

			// The entry already exists: QueueUserInputAsync does all its queue work before its first await, so the draft
			// can be dropped now even when an immediate entry then stops and drains the run.
			UserInputState.Content = string.Empty;
			UserInputState.Parts.Clear();

			await queued;
		}

		/// <summary>
		/// Drains the pending follow-ups as a turn: they are inserted in order and generate if any of them asked for it.
		/// </summary>
		public Task FlushFollowUpsAsync() =>
			Chat.Services.GetRequiredService<IChatOperationService>().FlushFollowUpsAsync(generate: false);

		/// <summary>
		/// Stops the running turn, waits for it to unwind, then drains the queue and generates again.
		/// </summary>
		public Task StopGenerationAndFlushAsync() =>
			Chat.Services.GetRequiredService<IChatOperationService>().StopAndFlushAsync();

		/// <summary>
		/// Stops the running turn and leaves the queue untouched.
		/// </summary>
		public Task StopGenerationAsync() =>
			Chat.Services.GetRequiredService<IChatOperationService>().StopGenerationAsync();

		/// <summary>
		/// Repeats the history's last turn: the tail is an unanswered user message, and the answer is asked for again
		/// without inserting anything new.
		/// </summary>
		public Task ResendTailAsync()
		{
			if (Chat.Messages.Count == 0)
				return Task.CompletedTask;

			return Chat.Services.GetRequiredService<IChatOperationService>().ResendMessageAsync(Chat.Messages.Count - 1);
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (disposing)
			{
				EditUserInputState.PropertyChanged -= OnUserInputStatePropertyChanged;
				UserInputState.PropertyChanged -= OnUserInputStatePropertyChanged;
				_executionTokens.ExecutionCancellationTokenChanged -= OnExecutionStateChanged;
				Chat.Messages.CollectionChanged -= OnChatMessagesChanged;
				_followUps.Changed -= OnFollowUpsChanged;
				Chips.Dispose();

				EditUserInputState.Dispose();
				_draftData.Dispose();
				_editData.Dispose();
			}
		}
	}
}
