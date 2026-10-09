namespace LLMDesktopAssistant.SlashCommands.Execution
{
	/// <summary>
	/// The generation intent ceiling of a command: a command may lower the caller's intent, never raise it.
	/// </summary>
	public static class SlashCommandIntent
	{
		/// <summary>
		/// Combines the caller's intent, the command's declarative ceiling and the executor's outcome.
		/// </summary>
		/// <param name="generateIntent">The caller's intent (the send button).</param>
		/// <param name="commandCeiling">
		/// The command's declarative ceiling — <see langword="null"/> leaves the intent untouched, <see langword="false"/>
		/// forbids generation (<see cref="SlashCommandInfo.Generate"/>).
		/// </param>
		/// <param name="executionOutcome">The executor's request.</param>
		/// <returns>The final generation decision.</returns>
		public static bool Resolve(bool generateIntent, bool commandCeiling, bool executionOutcome)
			=> generateIntent && commandCeiling && executionOutcome;
	}
}
