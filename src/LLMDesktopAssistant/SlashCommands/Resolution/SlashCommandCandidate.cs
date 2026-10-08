namespace LLMDesktopAssistant.SlashCommands.Resolution
{
	/// <summary>
	/// A command that matched a token, together with whether it lost to a higher-priority match.
	/// </summary>
	public readonly record struct SlashCommandCandidate(SlashCommandInfo Command, bool IsDefeated);
}
