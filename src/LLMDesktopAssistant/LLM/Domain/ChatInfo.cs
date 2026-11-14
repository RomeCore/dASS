using LLMDesktopAssistant.LLM.MVVM.Additional;

namespace LLMDesktopAssistant.LLM.Domain
{
	/// <summary>
	/// Represents information about a chat session. Used for display purposes or when saving/loading chat sessions.
	/// </summary>
	public class ChatInfo
	{
		/// <summary>
		/// Gets or sets the unique identifier for the chat session. Used mostly for database purposes.
		/// </summary>
		public required int Id { get; init; }

		/// <summary>
		/// Gets the title associated with the current instance.
		/// </summary>
		public required string Title { get; init; }

		/// <summary>
		/// Gets or sets the topic/category of the chat session.
		/// Human-readable category like "coding", "roleplay", "dnd", etc.
		/// </summary>
		public string Topic { get; init; } = string.Empty;

		/// <summary>
		/// Gets the creation timestamp for this instance. Used to track when the chat session was first created.
		/// </summary>
		public required DateTime CreatedAt { get; init; }

		/// <summary>
		/// Gets the last modified timestamp for this instance. Used to track when the chat session was last updated.
		/// </summary>
		public required DateTime LastModifiedAt { get; init; }

		/// <summary>
		/// Gets the parts attached to the chat itself, in their stored order - the chips the chat list renders under
		/// the title and the topic.
		/// </summary>
		public IReadOnlyList<AdditionalMessagePart> Parts { get; init; } = [];
	}
}