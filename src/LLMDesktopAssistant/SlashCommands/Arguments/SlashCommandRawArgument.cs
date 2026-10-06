namespace LLMDesktopAssistant.SlashCommands.Arguments
{
	/// <summary>
	/// A raw argument produced by the argument grammar, before any schema default is applied and before the value is
	/// validated and converted.
	/// </summary>
	public sealed class SlashCommandRawArgument
	{
		/// <summary>
		/// The schema slot the value belongs to, or <see langword="null"/> for a surplus positional
		/// (a positional beyond the declared ones).
		/// </summary>
		public SlashCommandArgument? Definition { get; init; }

		/// <summary>
		/// The raw value: with grouping quotes present (if any).
		/// </summary>
		public required string Raw { get; init; }

		/// <summary>
		/// The ready value: grouping quotes stripped and <c>\"</c> unescaped inside double quotes.
		/// </summary>
		public required string Unescaped { get; init; }

		/// <summary>
		/// Whether the value was written as a quoted group.
		/// </summary>
		public required bool WasQuoted { get; init; }
	}
}
