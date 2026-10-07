namespace LLMDesktopAssistant.SlashCommands.Arguments
{
	/// <summary>
	/// A bound command argument: the schema slot it belongs to, the raw argument the user wrote (if any) and the
	/// converted value.
	/// </summary>
	/// <remarks>
	/// This is the shape that reaches the executor through <c>SlashCommandExecutionContext</c>: defaults have been
	/// applied, the value has been validated and <see cref="ISlashCommandArgumentFormatProvider.Convert"/> has run.
	/// <see cref="Raw"/> stays <see langword="null"/> when the value did not come from the user, so it doubles as the
	/// provenance of <see cref="Ready"/>.
	/// </remarks>
	public sealed class ParsedSlashCommandArgument
	{
		/// <summary>
		/// The schema slot of the argument.
		/// </summary>
		public required SlashCommandArgument Definition { get; init; }

		/// <summary>
		/// The raw argument the user wrote — its text, its quotedness and its span in
		/// <see cref="SlashCommandArgumentsResult.RawArguments"/> — or <see langword="null"/> when the value came from
		/// <see cref="SlashCommandArgument.Default"/> or was not supplied at all.
		/// </summary>
		public SlashCommandRawArgument? Raw { get; init; }

		/// <summary>
		/// The text the value was made of: the user's unescaped text, or the schema default when the argument was not
		/// supplied. <see langword="null"/> for an optional argument that is absent and has no default.
		/// </summary>
		public string? Ready => Raw?.Unescaped ?? Definition.Default;

		/// <summary>
		/// The converted value: <see cref="SlashCommandArgument.Format"/>'s <c>Convert</c> result, <see cref="Ready"/>
		/// itself when the argument has no format provider, or <see langword="null"/> when an optional argument was not
		/// supplied and has no default.
		/// </summary>
		public object? Value { get; init; }
	}
}
