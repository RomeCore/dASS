namespace LLMDesktopAssistant.InputCompletion
{
	/// <summary>
	/// The kind of an input-completion state or item. Purely descriptive — the popup may use it to pick an icon, and
	/// nothing in the engine branches on it.
	/// </summary>
	public enum InputCompletionKind
	{
		/// <summary>No particular kind.</summary>
		None = 0,

		/// <summary>A slash command or one of its namespace segments.</summary>
		Command,

		/// <summary>An argument of a slash command.</summary>
		Argument,

		/// <summary>A chat-agent mention.</summary>
		Mention
	}
}
