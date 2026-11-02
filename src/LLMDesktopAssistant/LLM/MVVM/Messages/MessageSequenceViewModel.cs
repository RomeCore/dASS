using System.Collections.Specialized;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.MVVM;
using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.LLM.MVVM.Messages
{
	[ViewModelFor(typeof(MessageSequenceView))]
	public class MessageSequenceViewModel : ViewModelBase
	{
		/// <summary>
		/// Collection of message view models that represent the sequence of messages.
		/// </summary>
		public RangeObservableCollection<MessageViewModelBase> MessageViewModels { get; }

		/// <summary>
		/// The chat view model instance associated with this message sequence.
		/// </summary>
		public ChatViewModel ChatViewModel { get; }

		/// <summary>
		/// Raised when a message should be scrolled into view - the edit chip asks for it.
		/// </summary>
		public event Action<MessageViewModelBase>? BringIntoViewRequested;

		/// <summary>
		/// Asks the view to scroll the given message into view. Does nothing when the message is not in the sequence.
		/// </summary>
		/// <param name="message">The message to reveal.</param>
		public void BringIntoView(BranchedMessage message)
		{
			var index = ChatViewModel.Chat.Messages.IndexOf(message);
			if (index < 0 || index >= MessageViewModels.Count)
				return;

			BringIntoViewRequested?.Invoke(MessageViewModels[index]);
		}

		public MessageSequenceViewModel(ChatViewModel chatVM)
		{
			MessageViewModels = new RangeObservableCollection<MessageViewModelBase>();
			ChatViewModel = chatVM;

			MessageViewModels.AddRange(chatVM.Chat.Messages.Select(CreateMessageViewModel));
			chatVM.Chat.Messages.CollectionChanged += OnMessagesCollectionChanged;
		}

		private void OnMessagesCollectionChanged(object? s, NotifyCollectionChangedEventArgs e)
		{
			InvokeUI(() =>
			{
				switch (e.Action)
				{
					case NotifyCollectionChangedAction.Add:

						MessageViewModels.InsertRange(e.NewStartingIndex,
							e.NewItems!.Cast<BranchedMessage>().Select(CreateMessageViewModel));

						break;

					case NotifyCollectionChangedAction.Remove:

						for (int i = e.OldStartingIndex; i < e.OldStartingIndex + e.OldItems!.Count; i++)
							OnMessageViewModelRemoved(MessageViewModels[i]);
						MessageViewModels.RemoveRange(e.OldStartingIndex, e.OldItems!.Count);

						break;

					case NotifyCollectionChangedAction.Replace:

						for (int i = e.OldStartingIndex; i < e.OldStartingIndex + e.OldItems!.Count; i++)
							OnMessageViewModelRemoved(MessageViewModels[i]);
						MessageViewModels.RemoveRange(e.OldStartingIndex, e.OldItems!.Count);

						MessageViewModels.InsertRange(e.NewStartingIndex,
							e.NewItems!.Cast<BranchedMessage>().Select(CreateMessageViewModel));

						break;

					case NotifyCollectionChangedAction.Move:

						MessageViewModels.Move(e.OldStartingIndex, e.NewStartingIndex);

						break;

					case NotifyCollectionChangedAction.Reset:

						MessageViewModels.Clear();
						MessageViewModels.AddRange(ChatViewModel.Chat.Messages.Select(CreateMessageViewModel));

						break;
				}
				;
			});
		}

		private MessageViewModelBase CreateMessageViewModel(BranchedMessage branchedMessage)
		{
			var message = branchedMessage.Message;

			if (message is UserMessage)
			{
				return new UserMessageViewModel(branchedMessage, ChatViewModel);
			}
			else if (message is AssistantMessage)
			{
				return new AssistantMessageViewModel(branchedMessage, ChatViewModel);
			}
			else
			{
				throw new InvalidOperationException("Unsupported message type.");
			}
		}

		private void OnMessageViewModelRemoved(MessageViewModelBase viewModel)
		{
			viewModel.Dispose();
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (disposing)
			{
				foreach (var viewModel in MessageViewModels)
					viewModel.Dispose();
				ChatViewModel.Chat.Messages.CollectionChanged -= OnMessagesCollectionChanged;
				MessageViewModels.Clear();
			}
		}
	}
}