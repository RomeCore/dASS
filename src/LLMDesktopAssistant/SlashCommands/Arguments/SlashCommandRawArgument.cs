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

		/// <summary>
		/// The offset of the whole argument in <see cref="SlashCommandParsedArguments.RawArguments"/>: the token itself
		/// for a positional, the whole <c>key=value</c> for a keyed argument.
		/// </summary>
		public required int Position { get; init; }

		/// <summary>
		/// The length of the whole argument, so that <c>[Position, Position + Length)</c> covers it.
		/// </summary>
		public required int Length { get; init; }

		/// <summary>
		/// The offset of the argument's value. Equal to <see cref="Position"/> for a positional, because there the token
		/// <em>is</em> the value; for a keyed argument it points at the value that follows the <c>=</c>.
		/// </summary>
		public required int ValuePosition { get; init; }

		/// <summary>
		/// The length of the key part of a keyed argument — the identifier before the <c>=</c> — or 0 for a positional.
		/// </summary>
		public required int KeyLength { get; init; }

		/// <summary>
		/// The length of the value: <see cref="Position"/> plus <see cref="Length"/> minus
		/// <see cref="ValuePosition"/>. Always equal to <see cref="Raw"/>'s length.
		/// </summary>
		public int ValueLength => Position + Length - ValuePosition;
	}
}
