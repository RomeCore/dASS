using LiteDB;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.Settings;

namespace LLMDesktopAssistant.Data.ChatModels
{
	/// <summary>
	/// Represents a conversation model in the database.
	/// </summary>
	public sealed class ChatModel
	{
		/// <summary>
		/// The unique identifier for the сonversation.
		/// </summary>
		[BsonId]
		public int Id { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether this chat is temporary.
		/// </summary>
		public bool IsTemporary { get; set; } = false;

		/// <summary>
		/// Gets or sets the title that should be displayed to the user.
		/// </summary>
		public string Title { get; set; } = string.Empty;

		/// <summary>
		/// Gets or sets the topic/category of the chat.
		/// Human-readable category like "coding", "roleplay", "dnd", etc.
		/// </summary>
		public string Topic { get; set; } = string.Empty;

		/// <summary>
		/// Gets or sets the sub-topic of the chat: a narrower label inside the topic.
		/// </summary>
		public string SubTopic { get; set; } = string.Empty;

		/// <summary>
		/// Gets or sets a value indicating whether this chat is marked as a favorite.
		/// </summary>
		public bool Favorite { get; set; } = false;

		/// <summary>
		/// Get or sets the settings profile ID that is currently used by this chat.
		/// Used for accessing the <see cref=""/>
		/// </summary>
		public string SettingsProfile { get; set; } = ChatSettings.DefaultId;

		/// <summary>
		/// The ID of the root node in the chat tree. Can be -1 if there is no elements in the chat.
		/// </summary>
		public int RootNodeId { get; set; } = -1;

		/// <summary>
		/// The ID of the leaf node of the currently selected branch in the chat tree. Can be -1 if there is no elements in the chat.
		/// </summary>
		public int LeafNodeId { get; set; } = -1;

		/// <summary>
		/// The date and time when the chat was created.
		/// </summary>
		public DateTime CreatedAt { get; set; } = DateTime.Now;

		/// <summary>
		/// The date and time when the chat was last modified.
		/// </summary>
		public DateTime LastModifiedAt { get; set; } = DateTime.Now;

		/// <summary>
		/// Gets or sets the persisted user input state (draft text and parts) of this chat.
		/// Stored directly inside the chat document.
		/// </summary>
		public UserInputState? UserInputState { get; set; }
	}
}
