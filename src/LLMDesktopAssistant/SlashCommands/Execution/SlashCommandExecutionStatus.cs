namespace LLMDesktopAssistant.SlashCommands.Execution
{
	/// <summary>
	/// How a slash command invocation ended, as recorded in <see cref="SlashCommandFingerprint"/>.
	/// </summary>
	public enum SlashCommandExecutionStatus
	{
		/// <summary>The command ran and reported success.</summary>
		Executed = 0,

		/// <summary>The command did not run (an unresolved token, bad arguments) or failed at runtime.</summary>
		Failed = 1,

		/// <summary>The command was cancelled.</summary>
		Cancelled = 2
	}
}
