using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using LLMDesktopAssistant.Controls.Dialogs;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.LLM.MVVM
{
	/// <summary>
	/// One pending follow-up as the queue dialog shows it: a preview, who queued it, when, and at which level.
	/// </summary>
	public class FollowUpQueueItemViewModel : ViewModelBase
	{
		private const int PreviewLength = 120;

		/// <summary>
		/// Gets the queue entry this item shows.
		/// </summary>
		public FollowUpMessage Entry { get; }

		/// <summary>
		/// Gets the single-line preview of the message text.
		/// </summary>
		public string Preview { get; }

		/// <summary>
		/// Gets the sender line ("from {login}").
		/// </summary>
		public string Sender { get; }

		/// <summary>
		/// Gets the localized level caption.
		/// </summary>
		public string Level { get; }

		/// <summary>
		/// Gets the localized time the message was queued at.
		/// </summary>
		public string CreatedAt { get; }

		/// <summary>
		/// Removes this entry from the queue.
		/// </summary>
		public ICommand RemoveCommand { get; }

		/// <summary>
		/// Creates the item for one queue entry.
		/// </summary>
		/// <param name="entry">The queue entry.</param>
		/// <param name="remove">The dialog's removal callback.</param>
		public FollowUpQueueItemViewModel(FollowUpMessage entry, Action<FollowUpQueueItemViewModel> remove)
		{
			Entry = entry;

			var text = entry.Content.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ').Trim();
			Preview = text.Length <= PreviewLength ? text : text[..(PreviewLength - 3)] + "...";
			Sender = LocalizationManager.LocalizeStaticFormat("chat.follow_up.item.sender", entry.SenderLogin);
			Level = LocalizationManager.LocalizeStatic(LevelKey(entry.Level));
			CreatedAt = entry.CreatedAt.ToLocalTime().ToString("g");

			RemoveCommand = new AsyncRelayCommand(() =>
			{
				remove(this);
				return Task.CompletedTask;
			});
		}

		private static string LevelKey(FollowUpInjectionLevel level) => level switch
		{
			FollowUpInjectionLevel.Immediate => "chat.follow_up.level.immediate",
			FollowUpInjectionLevel.Message => "chat.follow_up.level.message",
			FollowUpInjectionLevel.Agent => "chat.follow_up.level.agent",
			_ => "chat.follow_up.level.agent_sequence",
		};
	}

	/// <summary>
	/// The dialog behind the queue chip: every pending follow-up in composition order, each removable on its own.
	/// The queue item stays the single source of truth, so the list follows its changes.
	/// </summary>
	[ViewModelFor(typeof(FollowUpQueueDialogView))]
	public class FollowUpQueueDialogViewModel : ViewModelBase
	{
		private readonly IFollowUpChatMessageQueue _queue;

		/// <summary>
		/// Gets the pending entries in composition order.
		/// </summary>
		public RangeObservableCollection<FollowUpQueueItemViewModel> Entries { get; } = [];

		/// <summary>
		/// Gets a value indicating whether there is anything to show.
		/// </summary>
		public bool HasEntries => Entries.Count > 0;

		/// <summary>
		/// Gets the dialog title.
		/// </summary>
		public string Title => LocalizationManager.LocalizeStatic("chat.follow_up.dialog.title");

		/// <summary>
		/// Gets the placeholder shown when the queue is empty.
		/// </summary>
		public string EmptyText => LocalizationManager.LocalizeStatic("chat.follow_up.dialog.empty");

		/// <summary>
		/// Closes the dialog.
		/// </summary>
		public ICommand CloseCommand { get; }

		/// <summary>
		/// Creates the dialog view model over the chat's queue.
		/// </summary>
		/// <param name="queue">The queue to show and mutate.</param>
		public FollowUpQueueDialogViewModel(IFollowUpChatMessageQueue queue)
		{
			_queue = queue;

			CloseCommand = new AsyncRelayCommand(() =>
			{
				DialogManager.CloseDialog();
				return Task.CompletedTask;
			});

			Reload();
			_queue.Changed += OnQueueChanged;
		}

		private void Reload()
		{
			Entries.Clear();

			foreach (var entry in _queue.Entries)
				Entries.Add(new FollowUpQueueItemViewModel(entry, Remove));

			RaisePropertyChanged(nameof(HasEntries));
		}

		private void Remove(FollowUpQueueItemViewModel item) => _queue.Remove(item.Entry);

		private void OnQueueChanged() => InvokeUI(Reload);

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (disposing)
				_queue.Changed -= OnQueueChanged;
		}
	}
}
