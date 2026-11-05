namespace LLMDesktopAssistant.Utils.Patterns
{
	/// <summary>
	/// One construct of a pattern, with the position it started at in the source text.
	/// </summary>
	/// <param name="Position">The zero-based index of the unit's first character in the source pattern.</param>
	public abstract record GlobUnit(int Position);

	/// <summary>
	/// A single literal character.
	/// </summary>
	/// <param name="Position">The zero-based index of the character in the source pattern.</param>
	/// <param name="Value">The literal character.</param>
	public sealed record GlobLiteral(int Position, char Value) : GlobUnit(Position);

	/// <summary>
	/// '<c>*</c>': any run of characters within the current scope.
	/// </summary>
	/// <param name="Position">The zero-based index of the unit in the source pattern.</param>
	public sealed record GlobAnyChars(int Position) : GlobUnit(Position);

	/// <summary>
	/// '<c>?</c>': exactly one character.
	/// </summary>
	/// <param name="Position">The zero-based index of the unit in the source pattern.</param>
	public sealed record GlobAnyChar(int Position) : GlobUnit(Position);

	/// <summary>
	/// '<c>**</c>': zero or more whole segments.
	/// </summary>
	/// <param name="Position">The zero-based index of the unit in the source pattern.</param>
	public sealed record GlobDoubleStar(int Position) : GlobUnit(Position);

	/// <summary>
	/// A separator. Whether it splits segments or is an ordinary character is decided by the
	/// dialect when the units are compiled, not here.
	/// </summary>
	/// <param name="Position">The zero-based index of the unit in the source pattern.</param>
	public sealed record GlobSeparator(int Position) : GlobUnit(Position);

	/// <summary>
	/// A character class, with its raw body between the brackets and the negation marker included.
	/// </summary>
	/// <param name="Position">The zero-based index of the opening bracket in the source pattern.</param>
	/// <param name="Body">The text between the brackets, for example <c>!abc</c> or <c>a-c</c>.</param>
	public sealed record GlobClass(int Position, string Body) : GlobUnit(Position);

	/// <summary>
	/// A brace group: a list of alternatives, each a list of units.
	/// </summary>
	/// <param name="Position">The zero-based index of the opening brace in the source pattern.</param>
	/// <param name="Alternatives">The branches between the braces, in source order.</param>
	public sealed record GlobBrace(int Position, IReadOnlyList<IReadOnlyList<GlobUnit>> Alternatives)
		: GlobUnit(Position);
}
