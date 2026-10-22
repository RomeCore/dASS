namespace LLMDesktopAssistant.Prompting.Context
{
	/// <summary>
	/// The kind of a prompt context: what produces its text and where that text lands.
	/// </summary>
	public enum PromptContextProviderKind
	{
		/// <summary>
		/// Unknown context provider kind.
		/// </summary>
		Unknown = 0,

		/// <summary>
		/// A native section rendered into the frozen system prompt header: the state is captured once per
		/// anchor and every change is announced as a delta. This is the default for non-scriptable contexts.
		/// </summary>
		Anchored,

		/// <summary>
		/// The script runs on every prompt build; its text is injected into the current request only.
		/// </summary>
		LiveTail,

		/// <summary>
		/// The script runs on every prompt build; its text is persisted as a stamp and re-injected
		/// whenever the previous stamp becomes stale.
		/// </summary>
		Supersede
	}
}
