using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.Services.Prompting;

namespace LLMDesktopAssistant.Hooks.Chat
{
	/// <summary>
	/// One message as a turn hook carries it: who sent it and what the model would read.
	/// </summary>
	/// <remarks>
	/// <see cref="Content"/> is the model-facing text - the raw content shaped by the producing command's mode and
	/// extended with the text of every additional part - so a hook sees what the agent sees, not what is rendered.
	/// </remarks>
	public sealed class ChatHookMessageInfo
	{
		/// <summary>
		/// The database id of the message, or <see langword="null"/> when it is not saved.
		/// </summary>
		public int? MessageId { get; init; }

		/// <summary>
		/// The role of the sender: <c>user</c>, <c>assistant</c> or <c>other</c>.
		/// </summary>
		public string Role { get; init; } = string.Empty;

		/// <summary>
		/// The login of the user or the id of the agent that sent the message.
		/// </summary>
		public string SenderId { get; init; } = string.Empty;

		/// <summary>
		/// The display name of the sender, or <see langword="null"/> when it does not resolve.
		/// </summary>
		public string? Sender { get; init; }

		/// <summary>
		/// The model-facing text of the message, or <see langword="null"/> when it contributes none.
		/// </summary>
		public string? Content { get; init; }

		/// <summary>
		/// Describes a history entry for the turn hooks.
		/// </summary>
		/// <param name="branched">The entry, which already carries the database id.</param>
		public static ChatHookMessageInfo Create(BranchedMessage branched) =>
			Create(branched.Message, branched.MessageId < 0 ? null : branched.MessageId);

		/// <summary>
		/// Describes a message for the turn hooks.
		/// </summary>
		/// <param name="message">The message.</param>
		/// <param name="messageId">Its database id, when the caller knows one.</param>
		public static ChatHookMessageInfo Create(ChatMessage message, int? messageId = null)
		{
			var (senderId, sender) = message switch
			{
				UserMessage user => (user.SenderLogin, (string?)user.Sender),
				AssistantMessage assistant => (assistant.SenderAgentId.ToString(), (string?)assistant.Sender),
				_ => (string.Empty, (string?)message.Sender)
			};

			return new ChatHookMessageInfo
			{
				MessageId = messageId,
				Role = message switch
				{
					UserMessage => "user",
					AssistantMessage => "assistant",
					_ => "other"
				},
				SenderId = senderId,
				Sender = sender,
				Content = MessageContentProjector.Project(message, MessagePartsFacet.Content)
			};
		}
	}
}
