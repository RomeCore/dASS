using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.Services.Instances;

namespace LLMDesktopAssistant.Hooks.Chat
{
	/// <summary>
	/// Fires <c>session.start</c> when a chat has been opened and <c>session.end</c> when it is closing.
	/// </summary>
	/// <remarks>
	/// Both points force fire-and-forget: the chat scope is built and torn down synchronously, so nobody can wait for
	/// a hook there. The consequence is worth stating plainly - at <c>session.end</c> the scope is disposed right
	/// after this handler returns, so a hook that has not resolved its chat services yet finds the chat gone and is
	/// logged and lost. A session hook has to be quick and to work with what it already holds.
	/// </remarks>
	[ChatService(typeof(IChatOpeningHandler))]
	[ChatService(typeof(IChatClosingHandler))]
	public class ChatSessionHooks(
		LLMDesktopAssistant.LLM.Domain.Chat chat,
		IChatCreationConfig creationConfig,
		IHookDispatcher hooks
	) : IChatOpeningHandler, IChatClosingHandler
	{
		/// <inheritdoc/>
		public void OnChatOpened() => Fire(ChatHookEvents.SessionStart);

		/// <inheritdoc/>
		public void OnChatClosing() => Fire(ChatHookEvents.SessionEnd);

		private void Fire(string hookType)
		{
			var payload = new SessionHookPayload
			{
				SessionId = chat.SessionId,
				ChatId = creationConfig.ChatId,
				Title = chat.Title,
				Topic = chat.Topic,
				MessageCount = chat.Messages.Count,
				CreatedAt = creationConfig.CreatedAt,
				IsTemporary = creationConfig.IsTemporary
			};

			hooks.ExecuteHooksFnF(payload, hookType, null);
		}
	}
}
