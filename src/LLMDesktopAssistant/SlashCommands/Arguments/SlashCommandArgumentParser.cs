using LLMDesktopAssistant.Localization;
using RCParsing;
using RCParsing.TokenPatterns;

namespace LLMDesktopAssistant.SlashCommands.Arguments
{
	/// <summary>
	/// Parses the raw argument text of a slash command into positional and keyed arguments, against a command's
	/// <see cref="SlashCommandArgumentSchema"/>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Contract: whitespace separates; <c>'…'</c> and <c>"…"</c> group and their quotes are stripped; <c>\</c> escapes
	/// <c>"</c> inside double quotes; a token is marked as "was quoted"; <c>key=…</c> starts a keyed argument only for a
	/// key declared in the schema's <see cref="SlashCommandArgumentSchema.Keyed"/> (an undeclared <c>key=value</c> stays
	/// plain text); an unquoted keyed value runs to the next declared key (spaces allowed), a quoted one is bounded by
	/// its quotes; everything else is positional; positionals precede the first key; with
	/// <see cref="SlashCommandArgumentSchema.HasRestPositional"/> the remainder is not split into positional tokens —
	/// declared <c>key=…</c> segments are extracted and the rest is delivered verbatim (quotes kept) as
	/// <see cref="SlashCommandArgumentsResult.RawPositionalArguments"/>.
	/// </para>
	/// </remarks>
	public static class SlashCommandArgumentParser
	{
		/// <summary>
		/// The RCParsing grammar that implements the contract above. The schema is passed to the grammar through a
		/// parsing parameter.
		/// </summary>
		public static Parser Parser { get; }

		static SlashCommandArgumentParser()
		{
			var builder = new ParserBuilder();

			static ParsedElement ExistingKeyMatchFunction(CustomTokenPattern self, string input,
				int start, int end, object? parameter, bool calculateIntermediateValue,
				ref ParsingError furthestError, TokenPattern[] children)
			{
				var identifierResult = children[0].Match(input, start, end, null, false, ref furthestError);
				if (!identifierResult.success)
					return identifierResult;

				start = identifierResult.endIndex;
				var eqResult = children[1].Match(input, start, end, null, false, ref furthestError);
				if (!eqResult.success)
					return eqResult;

				start = eqResult.endIndex;
				var valueResult = children[2].Match(input, start, end, null, false, ref furthestError);

				var schema = (SlashCommandArgumentSchema)parameter!;
				var key = (string)identifierResult.intermediateValue!;

				if (schema.Keyed.TryGetValue(key, out var argument))
				{
					var unescapedValue = valueResult.intermediateValue as string;
					var rawValue = input.Substring(valueResult.startIndex, valueResult.length);
					bool wasQuoted = unescapedValue is not null;

					return new ParsedElement(identifierResult.startIndex,
						valueResult.endIndex - identifierResult.startIndex,
						(key,
						new SlashCommandRawArgument
						{
							Definition = argument,
							WasQuoted = wasQuoted,
							Raw = rawValue,
							Unescaped = unescapedValue ?? string.Empty
						}));
				}

				return ParsedElement.Fail;
			}

			builder.CreateToken("key")
				.Custom(ExistingKeyMatchFunction,
					b => b.CaptureText(b => b.Identifier()),
					b => b.Literal('='),
					b => b.Token("positional_keyed"));

			builder.CreateToken("positional")
				.Choice(
					b => b.Token("positional_singlequote"),
					b => b.Token("positional_doublequote"),
					b => b.Token("positional_nonws")
				);

			builder.CreateToken("positional_keyed")
				.Choice(
					b => b.Token("positional_singlequote"),
					b => b.Token("positional_doublequote"),
					b => b.Empty()
				);

			builder.CreateToken("positional_nonws")
				.TextUntil(b => b.Char(char.IsWhiteSpace));

			builder.CreateToken("positional_singlequote")
				.Between(
					b => b.Literal('\''),
					b => b.EscapedText([KeyValuePair.Create("\\'", "'")], ["'"]),
					b => b.Literal('\'')
				);

			builder.CreateToken("positional_doublequote")
				.Between(
					b => b.Literal('\"'),
					b => b.EscapedText([KeyValuePair.Create("\\\"", "\"")], ["\""]),
					b => b.Literal('\"')
				);

			Parser = builder.Build();
		}

		/// <summary>
		/// Parses <paramref name="rawArguments"/> against <paramref name="schema"/>.
		/// </summary>
		/// <exception cref="InvalidOperationException">The argument text is not a valid argument list.</exception>
		public static SlashCommandArgumentsResult Parse(SlashCommandArgumentSchema schema, string rawArguments)
		{
			if (!TryParse(schema, rawArguments, out var result))
				throw new InvalidOperationException($"Failed to parse slash command arguments: {result.Error?.Key}.");

			return result;
		}

		/// <summary>
		/// Tries to parse <paramref name="rawArguments"/> against <paramref name="schema"/>.
		/// </summary>
		/// <returns>
		/// <see langword="false"/> when the text is not a valid argument list; <paramref name="error"/> then carries the
		/// reason (<c>command.error.parse_error</c>).
		/// </returns>
		public static bool TryParse(SlashCommandArgumentSchema schema, string rawArguments,
			out SlashCommandArgumentsResult result)
		{
			var keyedBuilder = ImmutableDictionary<string, SlashCommandRawArgument>.Empty;

			// Step 1: find all keys and their values
			if (!schema.Keyed.IsEmpty)
			{
				var allMatches = Parser.FindAllMatches("key", schema, overlap: false);

				(string, SlashCommandRawArgument)? lastKey = null;
				int lastKeyIndex = -1;

				foreach (var match in allMatches)
				{
					var (key, value) = ((string, SlashCommandRawArgument))match.IntermediateValue!;
					if (lastKey is null)
					{
						lastKey = (key, value);
						lastKeyIndex = match.EndIndex;
					}
					else
					{
						
					}
				}
			}

			throw new NotImplementedException(
				"The slash-command argument grammar is not implemented yet (ticket 07: " +
				"docs/issues/slash-commands-impl/issues/07-command-argument-grammar.md).");
		}
	}
}
