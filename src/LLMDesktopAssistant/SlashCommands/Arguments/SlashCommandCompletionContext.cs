namespace LLMDesktopAssistant.SlashCommands.Arguments
{
	/// <summary>
	/// The context of an argument-completion request.
	/// </summary>
	/// <remarks>
	/// The context deliberately carries only argument-level state for now: it gains the resolved command
	/// (<c>SlashCommandInfo Command</c>) in ticket 08, once that type exists.
	/// </remarks>
	public sealed class SlashCommandCompletionContext
	{
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
