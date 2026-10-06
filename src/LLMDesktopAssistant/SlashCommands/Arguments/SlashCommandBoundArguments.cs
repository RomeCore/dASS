using LLMDesktopAssistant.Localization;

namespace LLMDesktopAssistant.SlashCommands.Arguments
{
	/// <summary>
	/// The result of binding a <see cref="SlashCommandArgumentsResult"/> against a
	/// <see cref="SlashCommandArgumentSchema"/>.
	/// </summary>
	public sealed class SlashCommandBoundArguments
	{
		/// <summary>
		/// The bound positional arguments: exactly one entry per declared positional, in schema order. An optional
		/// positional that was neither supplied nor defaulted keeps its slot with an empty raw value and a
		/// <see langword="null"/> value, so indices stay aligned with the schema.
		/// </summary>
		public required ImmutableList<ParsedSlashCommandArgument> Positionals { get; init; }

		/// <summary>
		/// The bound keyed arguments, keyed by their declared key. A key that was neither supplied nor defaulted is
		/// absent (use <c>TryGetValue</c>).
		/// </summary>
		public required ImmutableDictionary<string, ParsedSlashCommandArgument> Keyed { get; init; }

		/// <summary>
		/// The rest positional's verbatim text.
		/// </summary>
		public required string RawPositionalArguments { get; init; }

		/// <summary>
		/// The blocking error, or <see langword="null"/> when the arguments bound successfully.
		/// </summary>
		/// <remarks>
		/// When set, the collections are empty: binding stops at the first problem and the send is blocked. The possible
		/// values are <c>command.error.parse_error</c>, <c>command.error.missing_argument</c>,
		/// <c>command.error.too_many_arguments</c> and <c>command.error.invalid_argument</c>.
		/// </remarks>
		public LocaleKeyBase? Error { get; init; }

		/// <summary>
		/// Whether the arguments bound successfully.
		/// </summary>
		public bool IsValid => Error is null;
	}
}
