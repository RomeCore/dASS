using LLMDesktopAssistant.LLM.MVVM.Additional;

namespace LLMDesktopAssistant.LLM.Domain
{
	/// <summary>
	/// Represents a shallow information about a chat session.
	/// </summary>
	public class ChatInfo
	{
		/// <summary>
		/// Unique identifier for the chat session in the database.
		/// </summary>
		public required int Id { get; init; }

		/// <summary>
		/// Title associated with the current instance.
		/// </summary>
		public required string Title { get; init; }

		/// <summary>
		/// Topic/category of the chat session.
		/// Human-readable category like "coding", "roleplay", "dnd", etc.
		/// </summary>
		public required string Topic { get; init; }

		/// <summary>
		/// Sub-topic of the chat session: the narrower label that sits inside <see cref="Topic"/>.
		/// </summary>
		public required string SubTopic { get; init; }

		/// <summary>
		/// A value indicating whether this chat session is marked as a favorite. Used for easy access and organization.
		/// </summary>
		public required bool Favorite { get; init; }

		/// <summary>
		/// Whether this chat session has an unsaved draft of user input.
		/// </summary>
		public required bool HasUserInputDraft { get; init; }

		/// <summary>
		/// Creation timestamp for this instance. Used to track when the chat session was first created.
		/// </summary>
		public required DateTime CreatedAt { get; init; }

		/// <summary>
		/// Last modified timestamp for this instance. Used to track when the chat session was last updated.
		/// </summary>
		public required DateTime LastModifiedAt { get; init; }

		/// <summary>
		/// Parts attached to the chat itself, in their stored order - the chips the chat list renders under
		/// the title and the topic.
		/// </summary>
		public IReadOnlyList<AdditionalMessagePart> Parts { get; init; } = [];
	}
}