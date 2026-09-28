namespace LLMDesktopAssistant.LLM.MVVM.Messages
{
	/// <summary>
	/// Enumerates the possible states of an <see cref="AssistantMessageViewModel"/>.
	/// </summary>
	public enum AssistantMessageState
	{
		/// <summary>
		/// The message is in its initial state.
		/// </summary>
		None,

		/// <summary>
		/// The message is currently being processed.
		/// </summary>
		Processing,

		/// <summary>
		/// The message has been completed.
		/// </summary>
		Completed
	}
}