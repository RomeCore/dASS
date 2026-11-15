namespace LLMDesktopAssistant.Hooks.Chat
{
	/// <summary>
	/// The user action that started a send, carried as the variant of the <c>message.user.submit.*</c> event.
	/// </summary>
	/// <remarks>
	/// The kind names the method the user invoked, not the path the message took afterwards: a direct send joins the
	/// follow-up queue when one is already pending, and stays <see cref="Direct"/>.
	/// </remarks>
	public enum UserSubmitKind
	{
		/// <summary>
		/// A send from the input box, which goes straight into the history when the queue is empty.
		/// </summary>
		Direct,

		/// <summary>
		/// A send that joins the follow-up queue instead of the history.
		/// </summary>
		Queue,

		/// <summary>
		/// A commit of an edited message.
		/// </summary>
		Edit
	}
}
