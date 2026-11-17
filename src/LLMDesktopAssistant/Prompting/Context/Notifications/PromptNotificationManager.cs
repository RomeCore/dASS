using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.Services;

namespace LLMDesktopAssistant.Prompting.Context.Notifications
{
	/// <inheritdoc cref="IPromptNotificationManager"/>
	[ChatService(typeof(IPromptNotificationManager))]
	public class PromptNotificationManager(
		Chat chat
	) : IPromptNotificationManager
	{
		/// <inheritdoc/>
		public void Push(PromptNotificationItem notification)
		{
			if (!chat.AdditionalData.TryGet<PromptNotificationPoolData>(out var pool))
				chat.AdditionalData.Add(pool = new());
			pool.AddNotification(notification);
		}
	}
}
