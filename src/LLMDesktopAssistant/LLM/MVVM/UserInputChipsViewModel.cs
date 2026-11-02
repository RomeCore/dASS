using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LLMDesktopAssistant.Controls.Dialogs;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.Localization;

namespace LLMDesktopAssistant.LLM.MVVM
{
	/// <summary>
	/// The chips above the input, shown as an overlay over the bottom of the history. Both chips are about the input:
	/// one holds the messages that were typed but not sent yet, the other marks that a message is loaded into the input
	/// for editing.
	/// <para/>
	/// A chip is a button, not a tab - it carries no selected state. A dismissible chip holds a second, nested close
	/// button: the queue chip's close clears the queue behind a confirmation, the edit chip's close cancels the edit.
	/// </summary>
	[ViewModelFor(typeof(UserInputChipsView))]
	public class UserInputChipsViewModel : ViewModelBase
	{
		private readonly UserInputViewModel _userInput;
		private readonly IFollowUpChatMessageQueue _queue;

		/// <summary>
		/// Gets the number of pending follow-ups.
		/// </summary>
		public int Count => _queue.Count;

		/// <summary>
		/// Gets a value indicating whether the queue chip should be shown.
		/// </summary>
		public bool HasQueue => Count > 0;

		/// <summary>
		/// Gets a value indicating whether the edit chip should be shown.
		/// </summary>
		public bool IsEditing => _userInput.EditingMessage != null;

		/// <summary>
		/// Gets a value indicating whether the chip row should be shown at all.
		/// </summary>
		public bool IsVisible => HasQueue || IsEditing;

		/// <summary>
		/// Gets the queue chip's caption, with the pending count.
		/// </summary>
		public string QueueTitle => LocalizationManager.LocalizeStaticFormat("chat.follow_up.chip", Count);

		/// <summary>
		/// Gets the edit chip's caption.
		/// </summary>
		public string EditTitle => LocalizationManager.LocalizeStatic("chat.follow_up.edit_chip");

		/// <summary>
		/// Opens the dialog listing the pending entries.
		/// </summary>
		public ICommand OpenQueueCommand { get; }

		/// <summary>
		/// Clears the whole queue, behind a confirmation.
		/// </summary>
		public ICommand ClearQueueCommand { get; }

		/// <summary>
		/// Scrolls the message being edited into view.
		/// </summary>
		public ICommand RevealEditCommand { get; }

		/// <summary>
		/// Cancels the edit and returns to the draft.
		/// </summary>
		public ICommand CancelEditCommand { get; }

		/// <summary>
		/// Creates the chips for one input.
		/// </summary>
		/// <param name="userInput">The input the chips belong to.</param>
		public UserInputChipsViewModel(UserInputViewModel userInput)
		{
			_userInput = userInput;
			_queue = userInput.Chat.Services.GetRequiredService<IFollowUpChatMessageQueue>();

			OpenQueueCommand = new AsyncRelayCommand(OpenQueueAsync);
			ClearQueueCommand = new AsyncRelayCommand(ClearQueueAsync);
			RevealEditCommand = new AsyncRelayCommand(RevealEditAsync);
			CancelEditCommand = new AsyncRelayCommand(CancelEditAsync);

			_queue.Changed += OnQueueChanged;
			_userInput.PropertyChanged += OnUserInputPropertyChanged;
		}

		private async Task OpenQueueAsync()
		{
			var dialog = new FollowUpQueueDialogViewModel(_queue);

			try
			{
				await DialogManager.ShowDialogAsync(dialog);
			}
			finally
			{
				dialog.Dispose();
			}
		}

		private async Task ClearQueueAsync()
		{
			var confirm = new ConfirmDialogViewModel
			{
				Title = LocalizationManager.LocalizeStatic("chat.follow_up.clear.title"),
				Description = LocalizationManager.LocalizeStatic("chat.follow_up.clear.description"),
				ConfirmText = LocalizationManager.LocalizeStatic("chat.follow_up.clear.action"),
				CancelText = LocalizationManager.LocalizeStatic("common.cancel"),
				IsDanger = true
			};

#pragma warning disable CS8605 // Unboxing is safe: the confirmation dialog always returns bool.
			var confirmed = (bool)await DialogManager.ShowDialogAsync(confirm);
#pragma warning restore CS8605

			if (confirmed)
				_queue.Clear();
		}

		private Task RevealEditAsync()
		{
			if (_userInput.EditingMessage is { } message)
				_userInput.ChatViewModel.MessageSequence.BringIntoView(message);

			return Task.CompletedTask;
		}

		private Task CancelEditAsync()
		{
			_userInput.CancelEditCommand.Execute(null);
			return Task.CompletedTask;
		}

		private void OnQueueChanged() => InvokeUI(() =>
		{
			RaisePropertyChanged(nameof(Count));
			RaisePropertyChanged(nameof(HasQueue));
			RaisePropertyChanged(nameof(IsVisible));
			RaisePropertyChanged(nameof(QueueTitle));
		});

		private void OnUserInputPropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName != nameof(UserInputViewModel.EditingMessage))
				return;

			InvokeUI(() =>
			{
				RaisePropertyChanged(nameof(IsEditing));
				RaisePropertyChanged(nameof(IsVisible));
			});
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (disposing)
			{
				_queue.Changed -= OnQueueChanged;
				_userInput.PropertyChanged -= OnUserInputPropertyChanged;
			}
		}
	}
}
