namespace LLMDesktopAssistant.SlashCommands.Arguments
{
	/// <summary>
	/// The argument schema of a command: the ordered positional arguments, the arguments addressed by name and the
	/// optional rest positional (the "big" argument that takes every token beyond the declared positionals).
	/// </summary>
	/// <remarks>
	/// Immutable on purpose - see <see cref="SlashCommandArgument"/>. <see cref="ImmutableList{T}"/> and
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
		/// The rest positional's slot, or <see langword="null"/> when the command has none: the tokens beyond the
		/// declared <see cref="Positionals"/> are not split further and are delivered verbatim (quotes kept) as
		/// <see cref="SlashCommandParsedArguments.RestPositional"/>.
		/// </summary>
		/// <remarks>
		/// The rest positional is an implicit slot that follows the declared positionals - it is not one of them and
		/// never appears in <see cref="SlashCommandParsedArguments.Positionals"/>. It is bound like any other argument:
		/// <see cref="SlashCommandArgument.Required"/>, <see cref="SlashCommandArgument.Default"/> and
		/// <see cref="SlashCommandArgument.Format"/> all apply, and the converted value lands in
		/// <see cref="SlashCommandBoundArguments.RestPositional"/>. The one thing that never happens is splitting: the
		/// whole surplus is a single, verbatim value - quotes kept - so a tail may legitimately carry spaces.
		/// </remarks>
		public SlashCommandArgument? RestPositional { get; init; }

		/// <summary>Whether the schema declares a rest positional (<see cref="RestPositional"/> is not null).</summary>
		public bool HasRestPositional => RestPositional is not null;
	}
}
