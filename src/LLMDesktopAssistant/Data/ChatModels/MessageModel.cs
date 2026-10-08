using LiteDB;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.Localization;

namespace LLMDesktopAssistant.Data.ChatModels
{
	/// <summary>
	/// Represents a message in a conversation inside a database.
	/// </summary>
	public class MessageModel
	{
		/// <summary>
		/// The unique identifier for the message.
		/// </summary>
		[BsonId]
		public int Id { get; set; }

		/// <summary>
		/// Gets or sets the time that this message is created at.
		/// </summary>
		public DateTime CreatedAt { get; set; } = DateTime.Now;

		/// <summary>
		/// Gets or sets the role of the message that describes its type.
		/// </summary>
		public RoleModel Role { get; set; }

		/// <summary>
		/// Gets or sets the status of the message.
		/// </summary>
		public MessageStatusModel Status { get; set; }

		/// <summary>
		/// Gets or sets the sender of the message. This can be GUID for assistant message and login name for user message.
		/// </summary>
		public string Sender { get; set; } = string.Empty;

		/// <summary>
		/// Gets or sets the ID of the agent stage associated with this message.
		/// </summary>
		public Guid AgentStageId { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether this assistant message is treated as a user message.
		/// </summary>
		public bool IsUserLike { get; set; }

		/// <summary>
		/// Gets or sets the visibility of the message. This can be used to control whether the message is visible to all users/agents or aonly specific.
		/// </summary>
		public MessageVisibility Visibility { get; set; } = MessageVisibility.Always;

		/// <summary>
		/// Gets or sets whether the message has been revealed to all users in the chat.
		/// </summary>
		public bool IsRevealed { get; set; }

		/// <summary>
		/// Gets or sets a list of users/agents that this message is visible to.
		/// </summary>
		public ImmutableList<string> VisibleTo { get; set; } = [];

		/// <summary>
		/// Gets or sets a value indicating whether this message is visible to white list users/agents.
		/// </summary>
		public bool IsVisibleToWhiteList { get; set; }

		/// <summary>
		/// Gets or sets the main content of the message.
		/// </summary>
		public string Content { get; set; } = string.Empty;

		/// <summary>
		/// Gets or sets the reasoning content of the assistant message.
		/// </summary>
		public string? ReasoningContent { get; set; }

		/// <summary>
		/// Gets or sets the error associated with the message, if any.
		/// </summary>
		public LocaleKeyBase? Error { get; set; }
	}
}