using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.Markdown.UINodes;
using LLMDesktopAssistant.Users;

namespace LLMDesktopAssistant.Markdown
{
	[ChatService]
	public class QuickActionService : Disposable
	{
		private readonly Chat _chat;
		private readonly IChatOperationService _chatOperator;
		private readonly IUserManagementService _userManager;

		public QuickActionService(Chat chat, IChatOperationService chatOperator, IUserManagementService userManager)
		{
			_chat = chat;
			_chatOperator = chatOperator;
			_userManager = userManager;

			var thisRef = new WeakReference<QuickActionService>(this);
			void OnActionClicked(string action, Chat chat)
			{
				if (!thisRef.TryGetTarget(out var @this))
				{
					QuickActionUiNode.OnActionClicked -= OnActionClicked;
					return;
				}

				if (@this._chat != chat)
					return;

				_ = @this._chatOperator.SendUserInputAsync(new UserInputState
				{
					Content = action,
					SenderLogin = @this._userManager.GetAvailableLocalUsers().FirstOrDefault()?.Login ??
						@this._userManager.GetLocalUsers().FirstOrDefault()?.Login ?? "user"
				}, generate: true);
			}
			QuickActionUiNode.OnActionClicked += OnActionClicked;
		}
	}
}