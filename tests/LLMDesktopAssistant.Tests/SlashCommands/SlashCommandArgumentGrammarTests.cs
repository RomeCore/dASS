using System.Collections.Immutable;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.SlashCommands.Arguments;

namespace LLMDesktopAssistant.Tests.SlashCommands
{
	/// <summary>
	/// The contract of the RCParsing argument grammar.
	/// </summary>
	public class SlashCommandArgumentGrammarTests
	{
		private static SlashCommandArgumentSchema Schema(bool restPositional = false, int positionals = 0,
			params string[] keys)
		{
			var keyed = ImmutableDictionary.CreateBuilder<string, SlashCommandArgument>();
			foreach (var key in keys)
				keyed[key] = new SlashCommandArgument { Name = Locale.GetKey($"test.argument.{key}") };

			var positionalArguments = ImmutableList.CreateBuilder<SlashCommandArgument>();
			for (var i = 0; i < positionals; i++)
				positionalArguments.Add(new SlashCommandArgument { Name = Locale.GetKey($"test.argument.positional{i}") });

			return new SlashCommandArgumentSchema
			{
				RestPositional = restPositional
					? new SlashCommandArgument { Name = Locale.GetKey("test.argument.rest") }
					: null,
				Positionals = positionalArguments.ToImmutable(),
				Keyed = keyed.ToImmutable()
			};
		}

		[Fact]
		public void Whitespace_SeparatesPositionals()
		{
			Assert.True(SlashCommandArgumentParser.TryParse(Schema(), "alpha beta", out var result));

			Assert.Null(result.Error);
			Assert.Equal(new[] { "alpha", "beta" }, result.Positionals.Select(p => p.Unescaped));
			Assert.Empty(result.Keyed);
		}

		[Fact]
		public void SingleAndDoubleQuotes_GroupAndAreStripped()
		{
			Assert.True(SlashCommandArgumentParser.TryParse(Schema(), "'a b' \"c d\"", out var result));

			Assert.Equal(new[] { "a b", "c d" }, result.Positionals.Select(p => p.Unescaped));
			Assert.All(result.Positionals, p => Assert.True(p.WasQuoted));
		}

		[Fact]
		public void Backslash_EscapesADoubleQuoteInsideDoubleQuotes()
		{
			Assert.True(SlashCommandArgumentParser.TryParse(Schema(), "\"a \\\"b\\\" c\"", out var result));

			var positional = Assert.Single(result.Positionals);
			Assert.Equal("a \"b\" c", positional.Unescaped);
		}

		[Fact]
		public void UndeclaredKey_StaysPlainText()
		{
			Assert.True(SlashCommandArgumentParser.TryParse(Schema(), "x = y + 1", out var result));

			Assert.Empty(result.Keyed);
			Assert.Equal(new[] { "x", "=", "y", "+", "1" }, result.Positionals.Select(p => p.Unescaped));
		}

		[Fact]
		public void UnquotedKeyedValue_RunsToTheNextDeclaredKey()
		{
			Assert.True(SlashCommandArgumentParser.TryParse(Schema(keys: ["a", "b"]), "a=one two b=three",
				out var result));

			Assert.Equal("one two", result.Keyed["a"].Unescaped);
			Assert.Equal("three", result.Keyed["b"].Unescaped);
		}

		[Fact]
		public void QuotedKeyedValue_IsBoundedByItsQuotes()
		{
			Assert.True(SlashCommandArgumentParser.TryParse(Schema(keys: ["a", "b"]), "a=\"one two\" b=three",
				out var result));

			Assert.Equal("one two", result.Keyed["a"].Unescaped);
			Assert.True(result.Keyed["a"].WasQuoted);
			Assert.Equal("three", result.Keyed["b"].Unescaped);
			Assert.False(result.Keyed["b"].WasQuoted);
		}

		[Fact]
		public void Mixed_PositionalsPrecedeTheFirstKey()
		{
			Assert.True(SlashCommandArgumentParser.TryParse(Schema(keys: ["a"]), "pos1 pos2 a=v", out var result));

			Assert.Equal(new[] { "pos1", "pos2" }, result.Positionals.Select(p => p.Unescaped));
			Assert.Equal("v", result.Keyed["a"].Unescaped);
		}

		[Fact]
		public void RestPositional_IsDeliveredVerbatimWithDeclaredKeysExtracted()
		{
			Assert.True(SlashCommandArgumentParser.TryParse(Schema(restPositional: true, keys: ["wait"]),
				"hello \"world\" wait=true", out var result));

			Assert.Equal("hello \"world\"", result.RawPositionalArguments);
			Assert.Equal("hello \"world\"", result.RestPositionalArguments);
			Assert.Equal("true", result.Keyed["wait"].Unescaped);
			Assert.Empty(result.Positionals);
		}

		[Fact]
		public void RestPositional_TakesTheTokensBeyondTheDeclaredPositionals()
		{
			Assert.True(SlashCommandArgumentParser.TryParse(Schema(restPositional: true, positionals: 1),
				"one two three", out var result));

			Assert.Null(result.Error);
			Assert.Equal(new[] { "one" }, result.Positionals.Select(p => p.Unescaped));
			Assert.Equal("one two three", result.RawPositionalArguments);
			Assert.Equal("two three", result.RestPositionalArguments);

			// The rest carries its schema slot and its span, so a caret can be located inside it.
			Assert.NotNull(result.RestPositional);
			var rest = result.RestPositional!;
			Assert.Equal("two three", rest.Raw);
			Assert.Equal(4, rest.Position);
			Assert.Equal(9, rest.Length);
			Assert.Equal(4, rest.ValuePosition);
			Assert.False(rest.WasQuoted);
			Assert.Equal("test.argument.rest", rest.Definition!.Name.Key);
		}

		[Fact]
		public void RestPositional_KeepsTheSurplusVerbatimQuotesIncluded()
		{
			Assert.True(SlashCommandArgumentParser.TryParse(Schema(restPositional: true, positionals: 1),
				"one 'two three'", out var result));

			Assert.Equal(new[] { "one" }, result.Positionals.Select(p => p.Unescaped));
			Assert.Equal("'two three'", result.RestPositionalArguments);
		}

		[Fact]
		public void RestPositional_WithoutSurplus_IsEmpty()
		{
			Assert.True(SlashCommandArgumentParser.TryParse(Schema(restPositional: true, positionals: 2),
				"one two", out var result));

			Assert.Equal(2, result.Positionals.Count);
			Assert.Equal("one two", result.RawPositionalArguments);
			Assert.Equal(string.Empty, result.RestPositionalArguments);
			Assert.Null(result.RestPositional);
		}

		[Fact]
		public void RestPositional_DoesNotFailOnAnUnterminatedQuoteInTheSurplus()
		{
			Assert.True(SlashCommandArgumentParser.TryParse(Schema(restPositional: true, positionals: 1),
				"one 'oops", out var result));

			Assert.Null(result.Error);
			Assert.Equal(new[] { "one" }, result.Positionals.Select(p => p.Unescaped));
			Assert.Equal("one 'oops", result.RawPositionalArguments);
			Assert.Equal("'oops", result.RestPositionalArguments);
		}

		[Fact]
		public void RestPositional_DeclaredPositionalIsStillQuoteChecked()
		{
			Assert.False(SlashCommandArgumentParser.TryParse(Schema(restPositional: true, positionals: 1),
				"'oops", out var result));

			Assert.Equal(0, result.ErrorPosition);
			Assert.Equal("command.error.parse_error", result.Error!.Key);
		}

		[Fact]
		public void RestPositional_SurplusStopsAtTheFirstDeclaredKey()
		{
			Assert.True(SlashCommandArgumentParser.TryParse(
				Schema(restPositional: true, positionals: 1, keys: ["wait"]), "one two wait=true", out var result));

			Assert.Equal(new[] { "one" }, result.Positionals.Select(p => p.Unescaped));
			Assert.Equal("one two", result.RawPositionalArguments);
			Assert.Equal("two", result.RestPositionalArguments);
			Assert.Equal("true", result.Keyed["wait"].Unescaped);
		}

		[Fact]
		public void RawPositionalArguments_KeepsTheWholeRegionBeforeTheFirstKeyVerbatim()
		{
			Assert.True(SlashCommandArgumentParser.TryParse(Schema(keys: ["a"]), "pos1 'pos 2' a=v", out var result));

			Assert.Equal(new[] { "pos1", "pos 2" }, result.Positionals.Select(p => p.Unescaped));
			Assert.Equal("pos1 'pos 2'", result.RawPositionalArguments);
			Assert.Equal(string.Empty, result.RestPositionalArguments);
		}

		[Fact]
		public void RawPositionalArguments_IsEmptyWhenOnlyKeysAreWritten()
		{
			Assert.True(SlashCommandArgumentParser.TryParse(Schema(keys: ["a"]), "a=v", out var result));

			Assert.Equal(string.Empty, result.RawPositionalArguments);
			Assert.Equal(string.Empty, result.RestPositionalArguments);
		}

		[Fact]
		public void RawArgumentSpans_LocatePositionals()
		{
			Assert.True(SlashCommandArgumentParser.TryParse(Schema(keys: ["a"]), "pos1 'pos 2' a=v", out var result));

			var first = result.Positionals[0];
			Assert.Equal((0, 4, 0, 0, 4), (first.Position, first.Length, first.ValuePosition, first.KeyLength,
				first.ValueLength));

			var second = result.Positionals[1];
			Assert.Equal((5, 7, 5, 0, 7), (second.Position, second.Length, second.ValuePosition, second.KeyLength,
				second.ValueLength));
		}

		[Fact]
		public void RawArgumentSpans_LocateKeyedValues()
		{
			Assert.True(SlashCommandArgumentParser.TryParse(Schema(keys: ["a", "b"]), "a=\"one two\" b=three",
				out var result));

			var quoted = result.Keyed["a"];
			Assert.Equal((0, 11, 2, 1, 9), (quoted.Position, quoted.Length, quoted.ValuePosition, quoted.KeyLength,
				quoted.ValueLength));

			var plain = result.Keyed["b"];
			Assert.Equal((12, 7, 14, 1, 5), (plain.Position, plain.Length, plain.ValuePosition, plain.KeyLength,
				plain.ValueLength));
		}

		[Fact]
		public void RawArgumentSpans_SkipWhitespaceAfterTheEqualsSign()
		{
			Assert.True(SlashCommandArgumentParser.TryParse(Schema(keys: ["a"]), "a=  one", out var result));

			var a = result.Keyed["a"];
			Assert.Equal("one", a.Raw);
			Assert.Equal(0, a.Position);
			Assert.Equal(7, a.Length);          // the whole "a=  one", the gap included
			Assert.Equal(4, a.ValuePosition);   // the value starts past the two spaces
			Assert.Equal(3, a.ValueLength);
		}

		[Fact]
		public void RawArgumentSpans_ValueLengthAlwaysMatchesRaw()
		{
			Assert.True(SlashCommandArgumentParser.TryParse(Schema(positionals: 2, keys: ["wait", "retries"]),
				"one 'two three' wait=\"yes please\" retries=3", out var result));

			Assert.NotEmpty(result.Positionals);
			Assert.NotEmpty(result.Keyed);
			Assert.All(result.Positionals, a => Assert.Equal(a.Raw.Length, a.ValueLength));
			Assert.All(result.Keyed.Values, a => Assert.Equal(a.Raw.Length, a.ValueLength));
		}

		[Fact]
		public void RawArguments_KeepsTheWholeTrimmedRemainder()
		{
			Assert.True(SlashCommandArgumentParser.TryParse(Schema(), "  alpha  beta  ", out var result));

			Assert.Equal("alpha  beta", result.RawArguments);
		}

		[Fact]
		public void UnterminatedQuote_IsAParseError()
		{
			Assert.False(SlashCommandArgumentParser.TryParse(Schema(), "'oops", out var result));

			Assert.NotNull(result.Error);
			Assert.Equal("command.error.parse_error", result.Error.Key);
			Assert.Equal(0, result.ErrorPosition);
		}

		[Fact]
		public void UnterminatedQuote_ReportsTheEarliestPosition()
		{
			Assert.False(SlashCommandArgumentParser.TryParse(Schema(), "abc bcd \"123 456", out var result));

			Assert.Equal("command.error.parse_error", result.Error!.Key);
			Assert.Equal(8, result.ErrorPosition);
		}
	}
}
