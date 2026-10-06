namespace LLMDesktopAssistant.SlashCommands.Arguments
{
	/// <summary>
	/// The argument schema of a command: the ordered positional arguments, the arguments addressed by name and whether
	/// the command takes a rest positional (the "big" argument).
	/// </summary>
	/// <remarks>
	/// Immutable on purpose — see <see cref="SlashCommandArgument"/>. <see cref="ImmutableList{T}"/> and
	/// <see cref="ImmutableDictionary{TKey,TValue}"/> are only shallowly immutable, which is why the leaf
	/// <see cref="SlashCommandArgument"/> is <c>init</c>-only as well.
	/// </remarks>
	public class SlashCommandArgumentSchema
	{
		/// <summary>
		/// The ordered positional arguments.
		/// </summary>
		public ImmutableList<SlashCommandArgument> Positionals { get; init; } = [];

		/// <summary>
		/// The arguments addressed by name, using the <c>key=value</c> syntax.
		/// </summary>
		public ImmutableDictionary<string, SlashCommandArgument> Keyed { get; init; } = [];

		/// <summary>
		/// Whether the schema declares a rest positional: the remainder of the argument text is not split into
		/// positional tokens and is delivered verbatim as
		/// <see cref="SlashCommandArgumentsResult.RawPositionalArguments"/>.
		/// </summary>
		public bool HasRestPositional { get; init; }
	}
}
