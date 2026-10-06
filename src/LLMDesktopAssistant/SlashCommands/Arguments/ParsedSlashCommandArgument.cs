namespace LLMDesktopAssistant.SlashCommands.Arguments
{
	/// <summary>
	/// A bound command argument: the schema slot it belongs to, its raw text and the converted value.
	/// </summary>
	/// <remarks>
	/// This is the shape that reaches the executor through <c>SlashCommandExecutionContext</c>: defaults have been
	/// applied, the value has been validated and <see cref="ISlashCommandArgumentFormatProvider.Convert"/> has run.
	/// </remarks>
	public sealed class ParsedSlashCommandArgument
	{
		/// <summary>
		/// The schema slot of the argument.
		/// </summary>
		public required SlashCommandArgument Definition { get; init; }

		/// <summary>
		/// The raw value (the user's text, or the schema default when the argument was not supplied).
		/// </summary>
		public required string Raw { get; init; }

		/// <summary>
		/// The converted value: <see cref="SlashCommandArgument.Format"/>'s <c>Convert</c> result, the raw string when
		/// the argument has no format provider, or <see langword="null"/> when an optional argument was not supplied
		/// and has no default.
		/// </summary>
		public object? Value { get; init; }
	}
}
