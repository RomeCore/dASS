namespace LLMDesktopAssistant.Hooks.Chat
{
	/// <summary>
	/// The moment of a user message insertion, carried as the variant of the <c>message.user.insert.*</c> event.
	/// </summary>
	public enum UserInsertPhase
	{
		/// <summary>
		/// The message is built but not written yet: <c>message.user.insert.pre</c>.
		/// </summary>
		Pre,

		/// <summary>
		/// The message has reached the history: <c>message.user.insert.post</c>.
		/// </summary>
		Post
	}
}
