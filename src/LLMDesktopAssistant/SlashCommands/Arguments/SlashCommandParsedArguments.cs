using LLMDesktopAssistant.Localization;

namespace LLMDesktopAssistant.SlashCommands.Arguments
{
	/// <summary>
	/// The output of the command argument grammar: the raw argument text split into positional and keyed arguments.
	/// </summary>
	/// <remarks>
	/// This is a purely syntactic result — no schema default has been applied, nothing has been validated or converted.
	/// Use <see cref="SlashCommandArgumentBinder"/> to turn it into <see cref="SlashCommandBoundArguments"/>.
	/// </remarks>
	public sealed class SlashCommandParsedArguments
	{
		/// <summary>
		/// The whole argument text, i.e. everything after the command token with the leading whitespace trimmed.
		/// </summary>
		public required string RawArguments { get; init; }

		/// <summary>
		/// The verbatim text of every positional argument: the whole region before the first declared key, with quotes
		/// and spacing kept. Empty when the argument text carries keyed segments only.
		/// </summary>
		public required string RawPositionalArguments { get; init; }

		/// <summary>
		/// The rest positional's text: the slice of <see cref="RawPositionalArguments"/> that lies beyond the declared
		/// positionals, delivered verbatim (quotes kept). Empty unless the schema declares
		/// <see cref="SlashCommandArgumentSchema.HasRestPositional"/>.
		/// </summary>
		public required string RestPositionalArguments { get; init; }

		/// <summary>
		/// The positional arguments, in the order they were written.
		/// </summary>
		public required ImmutableList<SlashCommandRawArgument> Positionals { get; init; }

		/// <summary>
		/// The arguments written as <c>key=value</c>, for keys declared in the schema.
		/// </summary>
		public required ImmutableDictionary<string, SlashCommandRawArgument> Keyed { get; init; }

		/// <summary>
		/// The position in the <see cref="RawArguments"/> where the error occurred, or -1 if there was no error.
		/// </summary>
		public int ErrorPosition { get; init; } = -1;

		/// <summary>
		/// A syntax-level error (an unterminated quote, for example), or <see langword="null"/> when the text parsed.
		/// </summary>
		public LocaleKeyBase? Error { get; init; }
	}
}
