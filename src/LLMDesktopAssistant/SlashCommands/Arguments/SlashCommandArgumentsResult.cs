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
	public sealed class SlashCommandArgumentsResult
	{
		/// <summary>
		/// The whole argument text, i.e. everything after the command token with the leading whitespace trimmed.
		/// </summary>
		public required string RawArguments { get; init; }

		/// <summary>
		/// The rest positional's text, delivered verbatim (quotes kept). Non-empty only when the schema declares
		/// <see cref="SlashCommandArgumentSchema.HasRestPositional"/>.
		/// </summary>
		public required string RawPositionalArguments { get; init; }

		/// <summary>
		/// The positional arguments, in the order they were written.
		/// </summary>
		public required ImmutableList<SlashCommandRawArgument> Positionals { get; init; }

		/// <summary>
		/// The arguments written as <c>key=value</c>, for keys declared in the schema.
		/// </summary>
		public required ImmutableDictionary<string, SlashCommandRawArgument> Keyed { get; init; }

		/// <summary>
		/// A syntax-level error (an unterminated quote, for example), or <see langword="null"/> when the text parsed.
		/// </summary>
		public LocaleKeyBase? Error { get; init; }
	}
}
