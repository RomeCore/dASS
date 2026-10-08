using System.Collections.Immutable;

namespace LLMDesktopAssistant.SlashCommands.Resolution
{
	/// <summary>
	/// A parsed command token: the slash-free word that opens a command message.
	/// </summary>
	/// <param name="Raw">The whole token as written, without the leading <c>/</c> marker.</param>
	/// <param name="Qualifiers">The namespace qualifiers — every segment before the name.</param>
	/// <param name="Name">The command name — the last segment.</param>
	/// <param name="IsValid">Whether the token is a syntactically valid command reference.</param>
	public readonly record struct SlashCommandToken(
		string Raw,
		ImmutableList<string> Qualifiers,
		string Name,
		bool IsValid);
}
