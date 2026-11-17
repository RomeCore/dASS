using LLMDesktopAssistant.Agents;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.Prompting.Context.Notifications;

namespace LLMDesktopAssistant.LLM.Services.Prompting
{
	[ChatService(typeof(IPromptNotificationProcessor))]
	public class PromptNotificationProcessor(
		Chat chat
	) : IPromptNotificationProcessor
	{
		/// <summary>
		/// Maximum number of notifications that agent can receive at once.
		/// Used to prevent notifications overflow (e.g. a new agent joins a chat).
		/// </summary>
		public const int MaxNotificationsAtOnce = 25;

		public void Process(ChatAgentDescriptor agent, EffectiveChatContext effectiveContext)
		{
			if (chat.Messages.Count == 0 ||
				chat.Messages[^1].Message is not AssistantMessage { IsCompleted: false } pendingAssistantMessage)
				throw new InvalidOperationException("Expected a pending assistant message, but none was found.");

			if (!chat.AdditionalData.TryGet<PromptNotificationPoolData>(out var pool))
				// Notifications pool is not initialized, skip processing.
				return;

			int seenNotifications = 0;

			// Walk through the entire history (not the effective) to gather seen notifications count.
			// This is needed to prevent notifications overflow on compactions (and other things that cuts history).
			for (int i = chat.Messages.Count - 2; i >= 0; i--)
			{
				var branchedMessage = chat.Messages[i];

				if (branchedMessage.Message is not AssistantMessage assistantMessage || assistantMessage.SenderAgentId != agent.Id)
					continue;

				if (assistantMessage.AdditionalData.TryGet<PromptNotificationMessageData>(out var notificationData))
				{
					seenNotifications = notificationData.SeenNotifications;
					break;
				}
			}

			int newNotificationsCount = pool.Count - seenNotifications;
			if (newNotificationsCount == 0)
				// No new notifications to process.
				return;

			var newNotifications = pool.GetLastNotifications(newNotificationsCount).ToList();
			List<string> visibleNotifications = [];

			var agentId = agent.Id;
			foreach (var notification in newNotifications)
				if (!notification.Cancelled && notification.VisibleTo.Contains(agentId)
					== notification.IsVisibleToWhiteList)
					visibleNotifications.Add(notification.Content);

			if (visibleNotifications.Count == 0)
				return;

			if (visibleNotifications.Count > MaxNotificationsAtOnce)
				visibleNotifications.RemoveRange(0, visibleNotifications.Count - MaxNotificationsAtOnce);

			pendingAssistantMessage.AdditionalData.TryReplace(new PromptNotificationMessageData
			{
				Snapshot = string.Join('\n', visibleNotifications),
				SeenNotifications = pool.Count
			});
		}
	}
}
