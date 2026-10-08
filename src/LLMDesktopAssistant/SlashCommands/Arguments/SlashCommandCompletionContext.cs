namespace LLMDesktopAssistant.SlashCommands.Arguments
{
	/// <summary>
	/// The context of an argument-completion request.
	/// </summary>
	/// <remarks>
	/// The context carries the resolved command alongside the argument-level state, so a format provider can complete
	/// against the whole command definition.
	/// </remarks>
	public sealed class SlashCommandCompletionContext
	{
		/// <summary>
		/// The resolved command whose argument is being completed.
		/// </summary>
		public required SlashCommandInfo Command { get; init; }
		/// <summary>
		/// The whole raw argument text typed so far (after the command token).
		/// </summary>
		public required string RawArguments { get; init; }

		/// <summary>
		/// The part of the argument value being completed.
		/// </summary>
		public required string CurrentPrefix { get; init; }
	}
}
