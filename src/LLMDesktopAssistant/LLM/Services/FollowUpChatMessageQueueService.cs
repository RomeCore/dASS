using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.MVVM.Additional.FollowUp;

namespace LLMDesktopAssistant.LLM.Services
{
	/// <inheritdoc cref="IFollowUpChatMessageQueue"/>
	[ChatService(typeof(IFollowUpChatMessageQueue))]
	public class FollowUpChatMessageQueueService(Chat chat) : IFollowUpChatMessageQueue
	{
		private readonly Lock _gate = new();

		/// <summary>
		/// The entry a drain currently owns. Runtime only: it covers the window between taking the head and its message
		/// reaching the history, and must not survive a restart.
		/// </summary>
		private FollowUpMessage? _inFlight;

		/// <inheritdoc/>
		public int Count => Item.Messages.Count;

		/// <inheritdoc/>
		public bool HasAny => Count > 0;

		/// <inheritdoc/>
		public IReadOnlyList<FollowUpMessage> Entries => Item.Messages;

		/// <inheritdoc/>
		public FollowUpInjectionLevel DefaultLevel
		{
			get => Item.DefaultLevel;
			set
			{
				Item.DefaultLevel = value;
			}
		}

		/// <inheritdoc/>
		public FollowUpInjectionLevel? MinLevel
		{
			get
			{
				var messages = Item.Messages;
				if (messages.Count == 0)
					return null;

				var min = (int)messages[0].Level;
				for (var i = 1; i < messages.Count; i++)
					min = Math.Min(min, (int)messages[i].Level);

				return (FollowUpInjectionLevel)min;
			}
		}

		/// <inheritdoc/>
		public event Action? Changed;

		/// <inheritdoc/>
		public bool HasEligible(FollowUpInjectionLevel barrier) => MinLevel is { } min && min <= barrier;

		/// <inheritdoc/>
		public FollowUpMessage Enqueue(UserInputState input, FollowUpInjectionLevel level, bool generateIntent,
			bool canExecuteCommand)
		{
			// The draft's parts are handed over, not copied: the same instances move into the history when the entry is
			// inserted, exactly as a direct send moves them.
			var entry = new FollowUpMessage
			{
				Level = level,
				GenerateIntent = generateIntent,
				CreatedAt = DateTime.Now,
				UserInput = input
			};

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

				var head = Item.Messages.FirstOrDefault();
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

				Item.Messages.Remove(entry);
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

				removed = Item.Messages.Remove(entry);
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
				Item.Messages.Clear();
			}

			Changed?.Invoke();
		}

		private FollowUpChatMessagesData Item
		{
			get
			{
				var existing = chat.AdditionalData.OfType<FollowUpChatMessagesData>().FirstOrDefault();
				if (existing is not null)
					return existing;

				var item = new FollowUpChatMessagesData();
				chat.AdditionalData.Add(item);
				return item;
			}
		}
	}
}
