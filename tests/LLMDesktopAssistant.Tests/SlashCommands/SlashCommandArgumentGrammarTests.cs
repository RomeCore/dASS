using System.Collections.Immutable;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.SlashCommands.Arguments;

namespace LLMDesktopAssistant.Tests.SlashCommands
{
	/// <summary>
	/// The contract of the RCParsing argument grammar.
	/// </summary>
	/// <remarks>
	/// The grammar is authored by the maintainer (the author of RCParsing), so these tests are skipped until it lands:
	/// un-skip them together with the grammar and make them green — that is the resolution criterion of ticket 07.
	/// </remarks>
	public class SlashCommandArgumentGrammarTests
	{
		private const string GrammarPending = "RCParsing grammar pending (maintainer)";

		private static SlashCommandArgumentSchema Schema(bool restPositional = false, params string[] keys)
		{
			var keyed = ImmutableDictionary.CreateBuilder<string, SlashCommandArgument>();
			foreach (var key in keys)
				keyed[key] = new SlashCommandArgument { Name = Locale.GetKey($"test.argument.{key}") };

			return new SlashCommandArgumentSchema
			{
				HasRestPositional = restPositional,
				Keyed = keyed.ToImmutable()
			};
		}

		[Fact(Skip = GrammarPending)]
		public void Whitespace_SeparatesPositionals()
		{
			Assert.True(SlashCommandArgumentParser.TryParse(Schema(), "alpha beta", out var result, out var error));

			Assert.Null(error);
			Assert.Equal(new[] { "alpha", "beta" }, result.Positionals.Select(p => p.Unescaped));
			Assert.Empty(result.Keyed);
		}

		[Fact(Skip = GrammarPending)]
		public void SingleAndDoubleQuotes_GroupAndAreStripped()
		{
			Assert.True(SlashCommandArgumentParser.TryParse(Schema(), "'a b' \"c d\"", out var result, out _));

			Assert.Equal(new[] { "a b", "c d" }, result.Positionals.Select(p => p.Unescaped));
			Assert.All(result.Positionals, p => Assert.True(p.WasQuoted));
		}

		[Fact(Skip = GrammarPending)]
		public void Backslash_EscapesADoubleQuoteInsideDoubleQuotes()
		{
			Assert.True(SlashCommandArgumentParser.TryParse(Schema(), "\"a \\\"b\\\" c\"", out var result, out _));

			var positional = Assert.Single(result.Positionals);
			Assert.Equal("a \"b\" c", positional.Unescaped);
		}

		[Fact(Skip = GrammarPending)]
		public void UndeclaredKey_StaysPlainText()
		{
			Assert.True(SlashCommandArgumentParser.TryParse(Schema(), "x = y + 1", out var result, out _));

			Assert.Empty(result.Keyed);
			Assert.Equal(new[] { "x", "=", "y", "+", "1" }, result.Positionals.Select(p => p.Unescaped));
		}

		[Fact(Skip = GrammarPending)]
		public void UnquotedKeyedValue_RunsToTheNextDeclaredKey()
		{
			Assert.True(SlashCommandArgumentParser.TryParse(Schema(keys: ["a", "b"]), "a=one two b=three",
				out var result, out _));

			Assert.Equal("one two", result.Keyed["a"].Unescaped);
			Assert.Equal("three", result.Keyed["b"].Unescaped);
		}

		[Fact(Skip = GrammarPending)]
		public void QuotedKeyedValue_IsBoundedByItsQuotes()
		{
			Assert.True(SlashCommandArgumentParser.TryParse(Schema(keys: ["a", "b"]), "a=\"one two\" b=three",
				out var result, out _));

			Assert.Equal("one two", result.Keyed["a"].Unescaped);
			Assert.True(result.Keyed["a"].WasQuoted);
			Assert.Equal("three", result.Keyed["b"].Unescaped);
			Assert.False(result.Keyed["b"].WasQuoted);
		}

		[Fact(Skip = GrammarPending)]
		public void Mixed_PositionalsPrecedeTheFirstKey()
		{
			Assert.True(SlashCommandArgumentParser.TryParse(Schema(keys: ["a"]), "pos1 pos2 a=v", out var result, out _));

			Assert.Equal(new[] { "pos1", "pos2" }, result.Positionals.Select(p => p.Unescaped));
			Assert.Equal("v", result.Keyed["a"].Unescaped);
		}

		[Fact(Skip = GrammarPending)]
		public void RestPositional_IsDeliveredVerbatimWithDeclaredKeysExtracted()
		{
			Assert.True(SlashCommandArgumentParser.TryParse(Schema(restPositional: true, keys: ["wait"]),
				"hello \"world\" wait=true", out var result, out _));

			Assert.Equal("hello \"world\"", result.RawPositionalArguments);
			Assert.Equal("true", result.Keyed["wait"].Unescaped);
			Assert.Empty(result.Positionals);
		}

		[Fact(Skip = GrammarPending)]
		public void RawArguments_KeepsTheWholeTrimmedRemainder()
		{
			Assert.True(SlashCommandArgumentParser.TryParse(Schema(), "  alpha  beta  ", out var result, out _));

			Assert.Equal("alpha  beta", result.RawArguments);
		}

		[Fact(Skip = GrammarPending)]
		public void UnterminatedQuote_IsAParseError()
		{
			Assert.False(SlashCommandArgumentParser.TryParse(Schema(), "'oops", out _, out var error));

			Assert.NotNull(error);
			Assert.Equal("command.error.parse_error", error.Key);
		}
	}
}
