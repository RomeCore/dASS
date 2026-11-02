using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.MVVM.Additional.FollowUp;

namespace LLMDesktopAssistant.LLM.Services
{
	/// <inheritdoc cref="IFollowUpChatMessageQueue"/>
	[ChatService(typeof(IFollowUpChatMessageQueue))]
	public class FollowUpChatMessageQueueService(Chat chat) : IFollowUpChatMessageQueue
	{
		private readonly object _gate = new();

		/// <summary>
		/// The entry a drain currently owns. Runtime only: it covers the window between taking the head and its message
		/// reaching the history, and must not survive a restart.
		/// </summary>
		private FollowUpMessage? _inFlight;

		/// <inheritdoc/>
		public event Action? Changed;

		/// <inheritdoc/>
		public int Count => Existing?.Messages.Count ?? 0;

		/// <inheritdoc/>
		public bool HasAny => Count > 0;

		/// <inheritdoc/>
		public IReadOnlyList<FollowUpMessage> Entries => Existing?.Messages.ToArray() ?? [];

		/// <inheritdoc/>
		public FollowUpInjectionLevel DefaultLevel
		{
			get => Existing?.DefaultLevel ?? FollowUpInjectionLevel.Message;
			set
			{
				// A write-back of the default value must not create the item: a chat with no pending messages stays clean.
				if (Existing is null && value == FollowUpInjectionLevel.Message)
					return;

				Item.DefaultLevel = value;
			}
		}

		/// <inheritdoc/>
		public FollowUpInjectionLevel? MinLevel
		{
			get
			{
				var messages = Existing?.Messages;
				if (messages is null || messages.Count == 0)
					return null;

				var min = (int)messages[0].Level;
				for (var i = 1; i < messages.Count; i++)
					min = Math.Min(min, (int)messages[i].Level);

				return (FollowUpInjectionLevel)min;
			}
		}

		/// <inheritdoc/>
		public bool HasEligible(FollowUpInjectionLevel barrier) => MinLevel is { } min && min <= barrier;

		/// <inheritdoc/>
		public FollowUpMessage Enqueue(UserInput input, FollowUpInjectionLevel level, bool generateIntent,
			bool canExecuteCommand)
		{
			// The draft's parts are handed over, not copied: the same instances move into the history when the entry is
			// inserted, exactly as a direct send moves them.
			var entry = new FollowUpMessage
			{
				Content = input.Content,
				SenderLogin = input.SenderLogin,
				Visibility = input.Visibility,
				VisibleTo = [.. input.VisibleTo],
				IsVisibleToWhiteList = input.IsVisibleToWhiteList,
				Level = level,
				GenerateIntent = generateIntent,
				CanExecuteCommand = canExecuteCommand,
				CreatedAt = DateTime.Now,
			};
			entry.Parts.Reset([.. input.Parts]);

			lock (_gate)
				Item.Messages.Add(entry);

			Changed?.Invoke();
			return entry;
		}

		/// <inheritdoc/>
		public bool TryBeginNext(out FollowUpMessage entry)
		{
			lock (_gate)
			{
				entry = null!;

				if (_inFlight is not null)
					return false;

				var head = Existing?.Messages.FirstOrDefault();
				if (head is null)
					return false;

				_inFlight = head;
				entry = head;
				return true;
			}
		}

		/// <inheritdoc/>
		public void Complete(FollowUpMessage entry)
		{
			lock (_gate)
			{
				if (ReferenceEquals(_inFlight, entry))
					_inFlight = null;

				Existing?.Messages.Remove(entry);
			}

			Changed?.Invoke();
		}

		/// <inheritdoc/>
		public void Abandon(FollowUpMessage entry)
		{
			lock (_gate)
			{
				if (ReferenceEquals(_inFlight, entry))
					_inFlight = null;
			}
		}

		/// <inheritdoc/>
		public void Remove(FollowUpMessage entry)
		{
			bool removed;

			lock (_gate)
			{
				if (ReferenceEquals(_inFlight, entry))
					_inFlight = null;

				removed = Existing?.Messages.Remove(entry) ?? false;
			}

			if (removed)
				Changed?.Invoke();
		}

		/// <inheritdoc/>
		public void Clear()
		{
			lock (_gate)
			{
				_inFlight = null;
				Existing?.Messages.Clear();
			}

			Changed?.Invoke();
		}

		/// <summary>
		/// The queue item as loaded, if the chat already carries one. Reading must never create it: a chat with no
		/// pending messages must not gain an empty item just because something asked for the count.
		/// </summary>
		private FollowUpChatMessagesData? Existing =>
			chat.AdditionalData.OfType<FollowUpChatMessagesData>().FirstOrDefault();

		/// <summary>
		/// The queue item, created and attached to the chat on first write.
		/// </summary>
		private FollowUpChatMessagesData Item
		{
			get
			{
				var existing = Existing;
				if (existing is not null)
					return existing;

				var item = new FollowUpChatMessagesData();
				chat.AdditionalData.Add(item);
				return item;
			}
		}
	}
}
