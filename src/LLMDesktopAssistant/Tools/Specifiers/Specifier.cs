using LLMDesktopAssistant.Utils.Files;
using LLMDesktopAssistant.Utils.Patterns;

namespace LLMDesktopAssistant.Tools.Specifiers
{
	/// <summary>
	/// A single part of a <see cref="Specifier"/> expression: either a standalone literal,
	/// a parameter-bound literal, or an AND-group of literals.
	/// </summary>
	public abstract class SpecifierPart
	{
	}

	/// <summary>
	/// A literal pattern that is matched against the parts of the main tool argument.
	/// The value is a flat glob pattern: <c>*</c> (any run of characters), <c>?</c> (exactly one),
	/// <c>[abc]</c>/<c>[!abc]</c> (a set), <c>{a,b}</c> (alternatives) and the terminal <c>:*</c>
	/// (the end of the text or a space and any arguments). A backslash is an ordinary character,
	/// and <c>[*]</c>/<c>[?]</c> are the way to spell a literal star or question mark.
	/// </summary>
	public class SpecifierLiteralPart : SpecifierPart
	{
		private GlobTextPattern? _glob;
		private GlobPattern? _pathGlob;

		/// <summary>
		/// The literal pattern value.
		/// </summary>
		public required string Value { get; init; }

		/// <summary>
		/// The zero-based index of <see cref="Value"/> in the specifier text it was parsed from.
		/// Used to report where in the specifier a broken pattern sits.
		/// </summary>
		public int Start { get; init; }

		/// <summary>
		/// The compiled pattern, built on first use: a rule that is never evaluated costs nothing,
		/// and a broken one throws only when it is actually matched. Throws <see cref="GlobException"/>
		/// for a pattern the flat dialect cannot honour.
		/// </summary>
		public GlobTextPattern Glob => _glob ??= GlobTextPattern.Parse(Value, GlobOptions.ArgumentTail);

		/// <summary>
		/// The compiled filesystem pattern, used when the literal targets a path. Built with
		/// <see cref="GlobPathMatcher.DefaultOptions"/>, so case sensitivity follows the file system.
		/// Throws <see cref="GlobException"/> for a pattern the path dialect cannot honour.
		/// </summary>
		public GlobPattern PathGlob => _pathGlob ??= GlobPattern.Parse(Value, GlobPathMatcher.DefaultOptions);

		public override string ToString()
		{
			return Value;
		}
	}

	/// <summary>
	/// A literal pattern bound to a named tool parameter (<c>name:value</c> syntax).
	/// Matched against the value of the parameter with the specified <see cref="Name"/>.
	/// </summary>
	public class SpecifierParameterPart : SpecifierLiteralPart
	{
		/// <summary>
		/// The name of the tool parameter this part is matched against.
		/// </summary>
		public required string Name { get; init; }

		public override string ToString()
		{
			return $"{Name}:{Value}";
		}
	}

	/// <summary>
	/// An AND-group of literals (<c>literal1 &amp;&amp; literal2</c> syntax).
	/// The group matches only when every literal matches at least one of its targets.
	/// </summary>
	public class SpecifierAndPart : SpecifierPart
	{
		/// <summary>
		/// The literals that must all match for the group to match.
		/// </summary>
		public required ImmutableList<SpecifierLiteralPart> Parts { get; init; }

		public override string ToString()
		{
			return string.Join(" && ", Parts.Select(p => p.ToString()));
		}
	}

	/// <summary>
	/// A parsed specifier expression: an OR-list of groups (<c>group1 || group2</c> syntax).
	/// The specifier matches when at least one of its groups matches.
	/// </summary>
	public class Specifier
	{
		/// <summary>
		/// The OR-groups of the specifier.
		/// </summary>
		public required ImmutableList<SpecifierPart> Parts { get; init; }

		/// <summary>
		/// Combines multiple specifiers into a single one by concatenating their parts.
		/// The resulting specifier matches when any of the combined specifiers matches.
		/// </summary>
		/// <param name="specifiers">The specifiers to combine. Cannot be <see langword="null"/>.</param>
		/// <returns>A new specifier whose parts are the concatenation of the input parts.</returns>
		public static Specifier Combined(params Specifier[] specifiers)
		{
			return new Specifier
			{
				Parts = [.. specifiers.SelectMany(s => s.Parts)]
			};
		}

		/// <summary>
		/// Combines multiple specifiers into a single one by concatenating their parts.
		/// The resulting specifier matches when any of the combined specifiers matches.
		/// </summary>
		/// <param name="specifiers">The specifiers to combine. Cannot be <see langword="null"/>.</param>
		/// <returns>A new specifier whose parts are the concatenation of the input parts.</returns>
		public static Specifier Combined(IEnumerable<Specifier> specifiers)
		{
			return new Specifier
			{
				Parts = [.. specifiers.SelectMany(s => s.Parts)]
			};
		}

		public override string ToString()
		{
			return string.Join(" || ", Parts.Select(p => p.ToString()));
		}
	}
}
