using System.Collections.Immutable;
using RCParsing;

namespace LLMDesktopAssistant.Utils.Patterns
{
	/// <summary>
	/// Parses glob syntax into a dialect-free unit list. The grammar knows nothing about
	/// separators being structural or about '<c>:*</c>' being an operator - both are decided when
	/// the units are compiled, which is what keeps the dialects from drifting apart.
	/// </summary>
	public static class GlobPatternParser
	{
		private static readonly Parser _parser;

		static GlobPatternParser()
		{
			var builder = new ParserBuilder();

			// Whitespace is a pattern character here, so whitespace skipping is left off on purpose.
			// A colon is deliberately not special: it is a literal, and the compiler turns a
			// trailing colon-and-star pair into the command-tail operator when the flag is on.

			builder.CreateRule("literal")
				.NegativeLookahead(b => b.LiteralChoice('*', '?', '[', ']', '{', '}', '/', '\\'))
				.AnyChar()
				.Transform(v => new GlobLiteral(v.StartIndex, v.Text[0]));

			// Inside a brace the comma and the closing brace terminate the alternative, so they are
			// not literals there.
			builder.CreateRule("brace-literal")
				.NegativeLookahead(b => b.LiteralChoice('*', '?', '[', ']', '{', '}', ',', '/', '\\'))
				.AnyChar()
				.Transform(v => new GlobLiteral(v.StartIndex, v.Text[0]));

			builder.CreateRule("separator")
				.LiteralChoice('/', '\\')
				.Transform(v => new GlobSeparator(v.StartIndex));

			builder.CreateRule("double-star")
				.Literal("**")
				.Transform(v => new GlobDoubleStar(v.StartIndex));

			builder.CreateRule("any-chars")
				.Literal('*')
				.Transform(v => new GlobAnyChars(v.StartIndex));

			builder.CreateRule("any-char")
				.Literal('?')
				.Transform(v => new GlobAnyChar(v.StartIndex));

			builder.CreateRule("class-item")
				.NegativeLookahead(b => b.Literal(']'))
				.AnyChar();

			// The first character of a class body may be ']', which is why the first item is a
			// separate choice. A body that never closes fails the whole rule, so the ordered
			// choice in the host rule falls back to a literal '['.
			builder.CreateRule("class")
				.Literal('[')
				.Optional(b => b.LiteralChoice('!', '^'))
				.Choice(b => b.Literal(']'), b => b.Rule("class-item"))
				.ZeroOrMore(b => b.Rule("class-item"))
				.Literal(']')
				.Transform(v => new GlobClass(v.StartIndex, v.Text[1..^1]));

			builder.CreateRule("open-bracket")
				.Literal('[')
				.Transform(v => new GlobLiteral(v.StartIndex, '['));

			builder.CreateRule("open-brace")
				.Literal('{')
				.Transform(v => new GlobLiteral(v.StartIndex, '{'));

			builder.CreateRule("close-bracket")
				.Literal(']')
				.Transform(v => new GlobLiteral(v.StartIndex, ']'));

			builder.CreateRule("close-brace")
				.Literal('}')
				.Transform(v => new GlobLiteral(v.StartIndex, '}'));

			builder.CreateRule("brace-unit")
				.Choice(
					b => b.Rule("brace"),
					b => b.Rule("double-star"),
					b => b.Rule("any-chars"),
					b => b.Rule("any-char"),
					b => b.Rule("class"),
					b => b.Rule("separator"),
					b => b.Rule("brace-literal"),
					b => b.Rule("open-bracket"),
					b => b.Rule("open-brace"),
					b => b.Rule("close-bracket"))
				.Transform(v => v.GetValue(0));

			builder.CreateRule("brace-units")
				.ZeroOrMore(b => b.Rule("brace-unit"))
				.Transform(v => v.Children
					.Select(c => c.GetValue<GlobUnit>())
					.ToImmutableList());

			builder.CreateRule("brace")
				.Literal('{')
				.OneOrMoreSeparated(b => b.Rule("brace-units"), b => b.Literal(','))
				.Literal('}')
				.Transform(v => new GlobBrace(v.StartIndex, v[1].Children
					.Select(c => c.GetValue<IReadOnlyList<GlobUnit>>())
					.ToImmutableList()));

			builder.CreateRule("unit")
				.Choice(
					b => b.Rule("brace"),
					b => b.Rule("double-star"),
					b => b.Rule("any-chars"),
					b => b.Rule("any-char"),
					b => b.Rule("class"),
					b => b.Rule("separator"),
					b => b.Rule("literal"),
					b => b.Rule("open-bracket"),
					b => b.Rule("open-brace"),
					b => b.Rule("close-bracket"),
					b => b.Rule("close-brace"))
				.Transform(v => v.GetValue(0));

			builder.CreateRule("units")
				.ZeroOrMore(b => b.Rule("unit"))
				.Transform(v => v.Children
					.Select(c => c.GetValue<GlobUnit>())
					.ToImmutableList());

			builder.CreateMainRule("pattern")
				.Rule("units")
				.EOF()
				.Transform(v => v[0].GetValue<IReadOnlyList<GlobUnit>>());

			_parser = builder.Build();
		}

		/// <summary>
		/// Parses the pattern into units, or throws when the grammar cannot describe it at all.
		/// </summary>
		/// <param name="pattern">The pattern text to parse.</param>
		/// <returns>The units in source order, with their positions.</returns>
		public static IReadOnlyList<GlobUnit> Parse(string pattern)
		{
			if (!_parser.TryParse<IReadOnlyList<GlobUnit>>(pattern, out var units))
				throw new GlobException(GlobError.InvalidSyntax, pattern, 0, "Invalid glob pattern.");

			return GlobExpander.NormalizeBraces(units);
		}

		/// <summary>
		/// Parses the pattern and expands its brace groups into flat alternatives.
		/// </summary>
		/// <param name="pattern">The pattern text to parse.</param>
		/// <returns>One unit list per alternative, in source order.</returns>
		public static IReadOnlyList<IReadOnlyList<GlobUnit>> ParseAlternatives(string pattern)
		{
			var units = Parse(pattern);
			return GlobExpander.Expand(units, pattern);
		}
	}
}
