namespace LLMDesktopAssistant.Prompting.Context.Notifications
{
	/// <summary>
	/// Represents a manager for pushing prompt notifications.
	/// </summary>
	public interface IPromptNotificationManager
	{
		/// <summary>
		/// Pushes a prompt notification to the pool for agents.
		/// </summary>
		/// <param name="notification">The notification to push.</param>
		void Push(PromptNotificationItem notification);
	}
}