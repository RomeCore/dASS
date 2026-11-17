namespace LLMDesktopAssistant.Prompting.Context.Notifications
{
	public class PromptNotificationItem : NotifyPropertyChanged
	{
		/// <summary>
		/// The time when the notification was created.
		/// </summary>
		public required DateTime CreatedAt { get; init; }

		/// <summary>
		/// The model-facing content of the notification.
		/// </summary>
		public required string Content { get; init; }

		/// <summary>
		/// The list of agents that are allowed to see this notification.
		/// </summary>
		public IReadOnlyList<Guid> VisibleTo { get; init; } = [];

		/// <summary>
		/// Whether <see cref="VisibleTo"/> is a whitelist, <see langword="false"/> for blacklist.
		/// </summary>
		public bool IsVisibleToWhiteList { get; init; } = false;

		/// <summary>
		/// Whether the notification has been cancelled and should not be shown.
		/// </summary>
		public bool Cancelled
		{
			get;
			set => SetProperty(ref field, value);
		}
	}
}
