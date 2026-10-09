namespace LLMDesktopAssistant.SlashCommands.Input
{
	/// <summary>
	/// How a leading command token currently reads — the state the highlighter colours and the popup reacts to.
	/// </summary>
	public enum SlashCommandInputResolutionState
	{
		/// <summary>The text is not a command (no leading <c>/</c>, or the <c>//</c> escape).</summary>
		None = 0,

		/// <summary>The token is still being typed: it is a prefix of at least one command, or empty.</summary>
		Partial = 1,

		/// <summary>The token is complete and nothing matches it.</summary>
		Unknown = 2,

		/// <summary>The token is complete and resolves to exactly one command.</summary>
		Known = 3,

		/// <summary>The token resolves, but a command won over others (the UI "ambiguous" state).</summary>
		WonOthers = 4
	}
}
