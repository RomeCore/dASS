namespace LLMDesktopAssistant.Hooks.Chat
{
	/// <summary>
	/// The document both session hooks receive: one opening of a chat.
	/// </summary>
	/// <remarks>
	/// The shape is the same at the start and at the end, so one body can serve both events and compare the two:
	/// a session is one runtime lifetime of a chat, not the persisted chat itself, and <see cref="SessionId"/> is
	/// what tells two openings of the same chat apart.
	/// </remarks>
	public sealed class SessionHookPayload
	{
		/// <summary>
		/// The runtime identity of this opening; the same chat opened again carries another one.
		/// </summary>
		public Guid SessionId { get; init; }

		/// <summary>
		/// The persisted chat, or <see langword="null"/> when the world has none.
		/// </summary>
		public int? ChatId { get; init; }

		/// <summary>
		/// The title of the chat.
		/// </summary>
		public string Title { get; init; } = string.Empty;

		/// <summary>
		/// The topic of the chat.
		/// </summary>
		public string Topic { get; init; } = string.Empty;

		/// <summary>
		/// How many messages the active branch holds: at the start what the chat was reopened with, at the end
		/// what the session produced.
		/// </summary>
		public int MessageCount { get; init; }

		/// <summary>
		/// When the chat was created, which is older than the session whenever a chat is reopened.
		/// </summary>
		public DateTime CreatedAt { get; init; }

		/// <summary>
		/// Whether the chat lives only in memory for this run and is never persisted.
		/// </summary>
		public bool IsTemporary { get; init; }
	}
}
