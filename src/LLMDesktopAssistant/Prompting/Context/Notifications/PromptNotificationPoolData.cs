using LiteDB;
using LLMDesktopAssistant.LLM.MVVM.Additional;
using LLMDesktopAssistant.Utils;
using Serilog;

namespace LLMDesktopAssistant.Prompting.Context.Notifications
{
	/// <summary>
	/// Represents the notification pool.
	/// </summary>
	public class PromptNotificationPoolData : AdditionalChatData
	{
		/// <summary>
		/// Maximum size of the notification pool.
		/// </summary>
		public const int MaxSize = 500;

		private readonly RangeObservableCollection<PromptNotificationItem> _notifications = [];
		/// <summary>
		/// The collection of notifications as a ring buffer.
		/// </summary>
		public RangeObservableCollection<PromptNotificationItem> Notifications
		{
			get => _notifications;
			set => _notifications.Reset(value);
		}

		/// <summary>
		/// The count of notifications that are lost due to the ring buffer being full.
		/// </summary>
		public int CutoffCount
		{
			get;
			set => SetProperty(ref field, value);
		}

		/// <summary>
		/// The current ring pointer, indicating the position in the ring buffer where the next notification will be added.
		/// Will be 0 until ring buffer is full.
		/// </summary>
		public int RingPointer
		{
			get;
			set => SetProperty(ref field, value);
		}

		/// <summary>
		/// The total count of notifications, including those that are lost due to the ring buffer.
		/// </summary>
		[BsonIgnore]
		public int Count => CutoffCount + Notifications.Count;

		public PromptNotificationPoolData()
		{
			IsVisible = false;
		}

		/// <summary>
		/// Adds a notification to the pool. If the pool is full, the oldest notification is removed.
		/// </summary>
		/// <param name="notification">The notification to add.</param>
		public void AddNotification(PromptNotificationItem notification)
		{
			if (Notifications.Count < MaxSize)
			{
				Notifications.Add(notification);
			}
			else
			{
				if (RingPointer < 0 || RingPointer >= MaxSize)
				{
					Log.Warning("Invalid ring pointer value: {RingPointer} (max ring size: {MaxSize}), clamping to 0",
						RingPointer, MaxSize);
					RingPointer = 0;
				}

				Notifications.Set(RingPointer, notification);
				RingPointer = (RingPointer + 1) % MaxSize;
				CutoffCount++;
			}
		}

		/// <summary>
		/// Enumerates all notifications (from optional start index) in the pool from oldest to newest.
		/// Lost notifications are not included.
		/// </summary>
		public IEnumerable<PromptNotificationItem> GetNotifications(int start = 0)
		{
			int pointer = RingPointer;
			for (int i = pointer + start; i < Notifications.Count; i++)
				yield return Notifications[i];
			if (pointer + start > Notifications.Count)
				start = pointer + start - Notifications.Count;
			else
				start = 0;
			for (int i = start; i < pointer; i++)
				yield return Notifications[i];
		}

		/// <summary>
		/// Enumerates all last <paramref name="count"/> notifications in the pool from oldest to newest.
		/// Lost notifications are not included.
		/// </summary>
		public IEnumerable<PromptNotificationItem> GetLastNotifications(int count)
		{
			return GetNotifications(Math.Max(0, Count - count));
		}
	}
}
