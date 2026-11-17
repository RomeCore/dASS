using LLMDesktopAssistant.LLM.MVVM.Additional;

namespace LLMDesktopAssistant.Prompting.Context.Notifications
{
	/// <summary>
	/// Represents a list of notifications seen by agent from last pull.
	/// This is a additional data that is attached to a pending agent message at the moment of creation.
	/// </summary>
	public class PromptNotificationMessageData : AdditionalChatData
	{
		/// <summary>
		/// The rendered notifications snapshot.
		/// </summary>
		public required string Snapshot { get; init; }

		/// <summary>
		/// The index of the last seen notification from the notification pool.
		/// </summary>
		public required int SeenNotifications { get; init; }

		public PromptNotificationMessageData()
		{
			IsVisible = false;
		}
	}
}
