namespace LLMDesktopAssistant.SlashCommands.Resolution
{
	/// <summary>
	/// The outcome of resolving a command token.
	/// </summary>
	public enum SlashCommandResolutionStatus
	{
		/// <summary>Nothing matched; the send is blocked.</summary>
		Unknown = 0,

		/// <summary>Exactly one command matched.</summary>
		Exact = 1,

		/// <summary>A command won over others (the UI state formerly called "ambiguous").</summary>
		WonOthers = 2
	}
}
