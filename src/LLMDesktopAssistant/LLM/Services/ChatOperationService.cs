using LLMDesktopAssistant.LLM.Domain;

namespace LLMDesktopAssistant.LLM.Services
{
	[ChatService(typeof(IChatOperationService))]
	public class ChatOperationService(
		Chat chat,
		IChatStorageService storage,
		IChatExecutionService executor,
		IChatExecutionTokenService tokens
		) : IChatOperationService
	{
		public async Task ContinueGenerationAsync(CancellationToken cancellationToken = default)
		{
			using var scope = tokens.WithToken(ChatExecutionLevel.Operation, cancellationToken, out var token);
			await executor.GenerateResponseAsync(token);
		}

		public async Task SendUserInputAsync(UserInput userInput, bool generate, CancellationToken cancellationToken = default)
		{
			using var scope = tokens.WithToken(ChatExecutionLevel.Operation, cancellationToken, out var token);

			storage.AppendMessage(CreateUserMessage(userInput));

			if (generate)
				await executor.GenerateResponseAsync(token);
		}

		public async Task SendEditedUserInputAsync(int messageIndex, UserInput userInput, bool generate, CancellationToken cancellationToken = default)
		{
			if (messageIndex < 0 || messageIndex >= chat.Messages.Count)
				throw new ArgumentOutOfRangeException(nameof(messageIndex));

			using var scope = tokens.WithToken(ChatExecutionLevel.Operation, cancellationToken, out var token);

			storage.EditMessage(messageIndex, CreateUserMessage(userInput));

			if (generate)
				await executor.GenerateResponseAsync(token);
		}

		public async Task RegenerateMessageAsync(int messageIndex, CancellationToken cancellationToken = default)
		{
			if (messageIndex < 0 || messageIndex >= chat.Messages.Count)
				throw new ArgumentOutOfRangeException(nameof(messageIndex));

			using var scope = tokens.WithToken(ChatExecutionLevel.Operation, cancellationToken, out var token);

			var targetMessage = chat.Messages[messageIndex].Message;

			if (targetMessage is UserMessage)
			{
				throw new InvalidOperationException("Cannot regenerate a user message.");
			}
			else if (targetMessage is AssistantMessage)
			{
				if (messageIndex < chat.Messages.Count)
					storage.PlaceNewBranch(messageIndex);
				await executor.GenerateResponseAsync(token);
			}
			else
			{
				throw new InvalidOperationException("Invalid message type.");
			}
		}

		public async Task ResendMessageAsync(int messageIndex, CancellationToken cancellationToken = default)
		{
			if (messageIndex < 0 || messageIndex >= chat.Messages.Count)
				throw new ArgumentOutOfRangeException(nameof(messageIndex));

			using var scope = tokens.WithToken(ChatExecutionLevel.Operation, cancellationToken, out var token);

			var nextMessageIndex = messageIndex + 1;
			if (nextMessageIndex < chat.Messages.Count)
				storage.PlaceNewBranch(nextMessageIndex);
			await executor.GenerateResponseAsync(token);
		}

		public void SwitchBranch(int messageIndex, int branchIndex)
		{
			if (messageIndex < 0 || messageIndex >= chat.Messages.Count)
				throw new ArgumentOutOfRangeException(nameof(messageIndex));
			if (branchIndex < 0 || branchIndex >= chat.Messages[messageIndex].AvailableBranchesCount)
				throw new ArgumentOutOfRangeException(nameof(branchIndex));

			using var scope = tokens.WithToken(ChatExecutionLevel.Operation, default, out _);
			storage.SwitchBranch(messageIndex, branchIndex);
		}

		public void EditMessage(int messageIndex, ChatMessage newMessage)
		{
			if (messageIndex < 0 || messageIndex >= chat.Messages.Count)
				throw new ArgumentOutOfRangeException(nameof(messageIndex));

			using var scope = tokens.WithToken(ChatExecutionLevel.Operation, default, out _);
			storage.EditMessage(messageIndex, newMessage);
		}

		public void DeleteMessageWithDescendants(int messageIndex)
		{
			if (messageIndex < 0 || messageIndex >= chat.Messages.Count)
				throw new ArgumentOutOfRangeException(nameof(messageIndex));

			using var scope = tokens.WithToken(ChatExecutionLevel.Operation, default, out _);
			storage.DeleteMessageWithDescendants(messageIndex);
		}

		private static UserMessage CreateUserMessage(UserInput userInput)
		{
			var userMessage = new UserMessage
			{
				CreatedAt = DateTime.Now,
				Content = userInput.Content,
				SenderLogin = userInput.SenderLogin,
				Visibility = userInput.Visibility,
				VisibleTo = userInput.VisibleTo,
				IsVisibleToWhiteList = userInput.IsVisibleToWhiteList
			};
			userMessage.AdditionalData.Reset(userInput.Parts);
			return userMessage;
		}
	}
}
