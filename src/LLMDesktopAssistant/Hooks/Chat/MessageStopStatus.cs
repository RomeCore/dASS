namespace LLMDesktopAssistant.Hooks.Chat
{
	/// <summary>
	/// The terminal status of an assistant message, carried as the variant of the
	/// <c>message.assistant.stop.*</c> event.
	/// </summary>
	public enum MessageStopStatus
	{
		/// <summary>
		/// The generation finished and the message is complete.
		/// </summary>
		Success,

		/// <summary>
		/// The generation failed: the message carries an error instead of an answer.
		/// </summary>
		Error,

		/// <summary>
		/// The turn was cancelled before the generation finished.
		/// </summary>
		Cancelled
	}
}
