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
	/// <see cref="SlashCommandArgumentSchema.HasRestPositional"/> the declared positionals are parsed as usual and the
	/// surplus is not split further — declared <c>key=…</c> segments are still extracted and that surplus is delivered
	/// verbatim (quotes kept) as <see cref="SlashCommandArgumentsResult.RestPositionalArguments"/>. Independently of the
	/// schema, <see cref="SlashCommandArgumentsResult.RawPositionalArguments"/> always carries every positional argument
	/// — the whole region before the first key, verbatim.
	/// </para>
	/// <para>
	/// Two edges of that contract are worth spelling out. A keyed value is bounded by its quotes <em>only</em> when
	/// nothing but whitespace follows the closing quote: <c>key="value" + junk</c> is not a quoted value but a literal
	/// one running to the next key (with the quotes as part of the text and <c>WasQuoted</c> unset) — the same thing
	/// <c>key=value + junk</c> produces, so quotes never change the outcome unless they wrap the value exactly. An
	/// opening quote that is never closed is a parse error at the earliest such quote, because the quote token then
	/// swallows the rest of the text. The rest positional is exempt from both rules: it is taken verbatim from the
	/// first token beyond the declared positionals, so an unbalanced quote inside it is nothing but text.
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

			// Whitespace separates and nothing else, so no rule ever needs to see the whitespace it skips. Skipping it
			// up front also matters for correctness: FindAllMatches starts a parse on every position, and on a
			// whitespace position "positional_nonws" would match empty and the walker would never advance.
			builder.Settings.SkipWhitespaces();

			static ParsedElement ExistingKeyMatchFunction(CustomTokenPattern self, string input,
				int start, int end, object? parameter, bool calculateIntermediateValue,
				ref ParsingError furthestError, TokenPattern[] children)
			{
				if (start > 0 && !char.IsWhiteSpace(input[start - 1])) // Ensure we start after a whitespace or start of input
					return ParsedElement.Fail;

				var identifierResult = children[0].Match(input, start, end, null, true, ref furthestError);
				if (!identifierResult.success)
					return identifierResult;

				start = identifierResult.endIndex;
				var eqResult = children[1].Match(input, start, end, null, false, ref furthestError);
				if (!eqResult.success)
					return eqResult;

				start = eqResult.endIndex;
				var valueResult = children[2].Match(input, start, end, null, true, ref furthestError);

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
							Unescaped = unescapedValue ?? string.Empty,
							// The span of the whole "key=value". An unquoted value is empty here, so its span is only
							// corrected once the next key is known — see Flush.
							Position = identifierResult.startIndex,
							Length = valueResult.endIndex - identifierResult.startIndex,
							ValuePosition = valueResult.startIndex,
							KeyLength = identifierResult.length
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
					b => b.Empty() // Leave empty to mark it "unquoted", text will be captured in main parsing algorithm
				);

			builder.CreateToken("positional_nonws")
				.TextUntil(b => b.Char(char.IsWhiteSpace), allowEmpty: false);

			builder.CreateToken("positional_singlequote")
				.Between(
					b => b.Literal('\''),
					b => b.EscapedText([KeyValuePair.Create("\\'", "'")], ["'"]),
					b => b.Optional(b => b.Literal('\''))
				);

			builder.CreateToken("positional_doublequote")
				.Between(
					b => b.Literal('\"'),
					b => b.EscapedText([KeyValuePair.Create("\\\"", "\"")], ["\""]),
					b => b.Optional(b => b.Literal('\"'))
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
			ArgumentNullException.ThrowIfNull(schema);
			ArgumentNullException.ThrowIfNull(rawArguments);

			var text = rawArguments.Trim();
			var keyedBuilder = ImmutableDictionary.CreateBuilder<string, SlashCommandRawArgument>();
			var positionalsBuilder = ImmutableList.CreateBuilder<SlashCommandRawArgument>();

			int errorPosition = -1;
			LocaleKeyBase? error = null;

			// Keep the earliest problem only: an unterminated quote swallows everything after itself, so any later
			// error is a consequence of it and would only point the user at the wrong place.
			void ReportError(int position)
			{
				if (errorPosition == -1 || position < errorPosition)
				{
					errorPosition = position;
					error = Locale.GetKey("command.error.parse_error");
				}
			}

			// Step 1: the declared key=value segments. Everything before the first one is positional, everything from
			// it on belongs to the keys. An unquoted value runs to the next declared key, so a value cannot be told
			// apart before all key positions are known — hence the scan first, the values afterwards.
			int firstKeyStart = text.Length;

			string? keyName = null;
			SlashCommandRawArgument? keyValue = null;
			int keyValueStart = 0;
			int keyValueEnd = 0;

			void Flush(int nextKeyStart)
			{
				if (keyName is null)
					return;

				var value = keyValue!;

				if (value.WasQuoted)
				{
					if (!IsTerminatedQuote(value.Raw))
					{
						// The opening quote was never closed: the quote token swallowed the rest of the text.
						ReportError(keyValueStart);
						return;
					}

					// Quoted means "bounded by its quotes" only while nothing but whitespace follows them.
					if (string.IsNullOrWhiteSpace(text[keyValueEnd..nextKeyStart]))
					{
						keyedBuilder[keyName] = value;
						return;
					}
				}

				// Unquoted, or a quoted value with trailing junk: one literal value running to the next key, quotes
				// and all.
				// An unquoted value is captured by the grammar as empty, so its span has to be cut here — from just
				// after the '=' to the next key, whitespace trimmed. The key's own offset comes from the match.
				var valueStart = keyValueStart;
				var valueEnd = nextKeyStart;
				while (valueStart < valueEnd && char.IsWhiteSpace(text[valueStart]))
					valueStart++;
				while (valueEnd > valueStart && char.IsWhiteSpace(text[valueEnd - 1]))
					valueEnd--;

				var raw = text[valueStart..valueEnd];
				keyedBuilder[keyName] = new SlashCommandRawArgument
				{
					Definition = value.Definition,
					Raw = raw,
					Unescaped = raw,
					WasQuoted = false,
					Position = value.Position,
					Length = valueEnd - value.Position,
					ValuePosition = valueStart,
					KeyLength = value.KeyLength
				};
			}

			if (!schema.Keyed.IsEmpty)
			{
				foreach (var match in Parser.FindAllMatches("key", text, schema, overlap: false))
				{
					if (keyName is null)
						firstKeyStart = match.StartIndex;

					var (key, value) = ((string, SlashCommandRawArgument))match.IntermediateValue!;
					Flush(match.StartIndex);

					keyName = key;
					keyValue = value;
					keyValueStart = match.StartIndex + key.Length + 1; // the identifier, then '=', then the value
					keyValueEnd = match.EndIndex;
				}

				Flush(text.Length);
			}

			// Step 2: the positionals, which are exactly the tokens before the first key. A rest schema parses the
			// declared positionals as usual and stops at the first surplus token: that one and everything after it is the
			// rest positional, taken verbatim — quotes kept, never checked for a closing one.
			var prefix = text[..firstKeyStart];
			var restStart = prefix.Length;
			var index = 0;

			foreach (var match in Parser.FindAllMatches("positional", prefix, overlap: false))
			{
				if (schema.HasRestPositional && index >= schema.Positionals.Count)
				{
					restStart = match.StartIndex;
					break;
				}

				var raw = prefix.Substring(match.StartIndex, match.Length);
				var wasQuoted = raw.Length > 0 && (raw[0] == '\'' || raw[0] == '"');

				if (wasQuoted && !IsTerminatedQuote(raw))
				{
					ReportError(match.StartIndex);
					continue;
				}

				positionalsBuilder.Add(new SlashCommandRawArgument
				{
					Definition = index < schema.Positionals.Count ? schema.Positionals[index] : null,
					Raw = raw,
					Unescaped = wasQuoted ? match.IntermediateValue as string ?? raw[1..^1] : raw,
					WasQuoted = wasQuoted,
					// A positional token is its own value, so the value span is the token span.
					Position = match.StartIndex,
					Length = match.Length,
					ValuePosition = match.StartIndex,
					KeyLength = 0
				});
				index++;
			}

			// Every positional argument, verbatim: the whole region before the first key, quotes and spacing kept. The
			// rest positional is a slice of that region — the surplus beyond the declared positionals.
			var rawPositionals = prefix.TrimEnd();
			var restPositionals = schema.HasRestPositional ? prefix[restStart..].TrimEnd() : string.Empty;

			if (error is not null)
			{
				result = new SlashCommandArgumentsResult
				{
					RawArguments = text,
					RawPositionalArguments = rawPositionals,
					RestPositionalArguments = restPositionals,
					Positionals = [],
					Keyed = [],
					ErrorPosition = errorPosition,
					Error = error
				};
				return false;
			}

			result = new SlashCommandArgumentsResult
			{
				RawArguments = text,
				RawPositionalArguments = rawPositionals,
				RestPositionalArguments = restPositionals,
				Positionals = positionalsBuilder.ToImmutable(),
				Keyed = keyedBuilder.ToImmutable()
			};
			return true;
		}

		/// <summary>
		/// Whether <paramref name="raw"/> is a quote-grouped token whose closing quote really terminates it: the
		/// opening and the closing quote are the same character, and the closing one is not escaped by a backslash.
		/// </summary>
		private static bool IsTerminatedQuote(string raw)
		{
			if (raw.Length < 2)
				return false;

			var quote = raw[0];
			if ((quote != '\'' && quote != '"') || raw[^1] != quote)
				return false;

			var backslashes = 0;
			for (var i = raw.Length - 2; i >= 0 && raw[i] == '\\'; i--)
				backslashes++;

			return backslashes % 2 == 0;
		}
	}
}
