using System.Text.Json.Nodes;
using RCParsing;
using RCParsing.TokenPatterns;

namespace LLMDesktopAssistant.Utils
{
	/// <summary>
	/// Represents a parser that will consume ANY text that looks like a JSON.
	/// Supports wrong escaped characters, missing quotes, unclosed brackets and more.
	/// </summary>
	public static class TolerantJsonParser
	{
		private class TolerantJsonEscapingStrategy : EscapingStrategy
		{
			public bool IsQuoteSingle { get; set; }

			public override bool TryEscape(string input, int position, int maxPosition, out int consumedLength, out string replacement)
			{
				if (input[position] != '\\' || position + 1 >= maxPosition)
				{
					consumedLength = 0;
					replacement = "";
					return false;
				}

				position++;
				char escapeChar = input[position];
				switch (escapeChar)
				{
					case '\\':
						consumedLength = 2;
						replacement = "\\";
						return true;

					case '"':
						consumedLength = 2;
						replacement = "\"";
						return true;

					case '\'':
						consumedLength = 2;
						replacement = "'";
						return true;

					case '0':
						consumedLength = 2;
						replacement = "\0";
						return true;

					case 'n':
						consumedLength = 2;
						replacement = "\n";
						return true;

					case 't':
						consumedLength = 2;
						replacement = "\t";
						return true;

					case 'r':
						consumedLength = 2;
						replacement = "\r";
						return true;

					case 'f':
						consumedLength = 2;
						replacement = "\f";
						return true;

					case 'b':
						consumedLength = 2;
						replacement = "\b";
						return true;

					case 'x':
					case 'u':
						position++;
						if (position >= maxPosition)
						{
							consumedLength = 0;
							replacement = "";
							return false;
						}

						int codePoint = 0;
						int consumedDigits = 0;
						// 'position' already points at the first hex digit, so the run starts here and its
						// length is what is left of the input. Starting at position + 1 dropped the first
						// digit: '\u0440' decoded from "440" (right by accident, a leading zero is free)
						// and consumed 5 of its 6 characters, leaving the last digit to be emitted as text -
						// 'р' became "р0", and '\u4E2D' turned into the wrong character entirely.
						int maxCount = Math.Min(escapeChar == 'x' ? 2 : 4, maxPosition - position);

						for (int i = position; i < position + maxCount; i++)
						{
							var c = input[i];

							if (c >= '0' && c <= '9')
							{
								consumedDigits++;
								codePoint *= 16;
								codePoint += c - '0';
							}
							else if (c >= 'A' && c <= 'F')
							{
								consumedDigits++;
								codePoint *= 16;
								codePoint += c - 'A' + 10;
							}
							else if (c >= 'a' && c <= 'f')
							{
								consumedDigits++;
								codePoint *= 16;
								codePoint += c - 'a' + 10;
							}
							else
							{
								break;
							}
						}

						consumedLength = 2 + consumedDigits;

						// A lone surrogate half is legal in JSON only as part of a pair, and
						// ConvertFromUtf32 throws on it. Emit the code unit as-is: two consecutive
						// escapes then join into the pair they were escaped from. A tolerant parser must
						// not throw on input it was built to tolerate.
						replacement = codePoint is >= 0xD800 and <= 0xDFFF
							? ((char)codePoint).ToString()
							: char.ConvertFromUtf32(codePoint);
						return true;
				}

				consumedLength = 0;
				replacement = "";
				return false;
			}

			public override bool TryStop(string input, int position, int maxPosition, out int consumedLength)
			{
				consumedLength = 1;
				return position < maxPosition && (IsQuoteSingle ? input[position] == '\'' : input[position] == '"');
			}
		}

		private static readonly Parser _parser;

		static TolerantJsonParser()
		{
			var builder = new ParserBuilder();

			builder.Settings.Skip(s => s
				.Choice(
					b => b.Literal("//").TextUntil('\n', '\r').Newline(),
					b => b.Literal("/*").TextUntil("*/", consumeStopSequence: true),
					b => b.Whitespaces()
				).ConfigureForSkip(), ParserSkippingStrategy.SkipBeforeParsingGreedy);

			builder.CreateToken("literal")
				.LiteralChoice([
					("null", null),
					("true", true),
					("false", false),
					("nan", double.NaN),
					("inf", double.PositiveInfinity),
					("infinite", double.PositiveInfinity),
					("infinity", double.PositiveInfinity),
					("+inf", double.PositiveInfinity),
					("+infinite", double.PositiveInfinity),
					("+infinity", double.PositiveInfinity),
					("-inf", double.NegativeInfinity),
					("-infinite", double.NegativeInfinity),
					("-infinity", double.NegativeInfinity),
				], StringComparer.OrdinalIgnoreCase);

			builder.CreateToken("number")
				.Number<double>();

			builder.CreateToken("string")
				.Between(
					s => s.Literal('"'),
					c => c.EscapedText(new TolerantJsonEscapingStrategy { IsQuoteSingle = false }),
					e => e.Optional(b => b.Literal('"'))
				);

			builder.CreateToken("sqstring") // Single quoted string
				.Between(
					s => s.Literal('\''),
					c => c.EscapedText(new TolerantJsonEscapingStrategy { IsQuoteSingle = true }),
					e => e.Optional(b => b.Literal('\''))
				);

			// We just need to trim end because parser already skipped the whitespaces
			builder.CreateToken("anystr_array")
				.First(
					b => b.Map<string>(b => b.TextUntil([']', ','], allowsEmpty: false), str => str.TrimEnd()),
					b => b.Optional(b => b.LiteralChoice('"', '\''))
				);

			builder.CreateToken("anystr_object")
				.First(
					b => b.Map<string>(b => b.TextUntil(['}', ':', ','], allowsEmpty: false), str => str.TrimEnd()),
					b => b.Optional(b => b.LiteralChoice('"', '\''))
				);

			builder.CreateRule("value_array")
				.Choice(
					b => b.Token("literal"),
					b => b.Token("number"),
					b => b.Token("string"),
					b => b.Token("sqstring"),
					b => b.Rule("array"),
					b => b.Rule("object"),
					b => b.Token("anystr_array")
				);

			builder.CreateRule("value_object")
				.Choice(
					b => b.Token("literal"),
					b => b.Token("number"),
					b => b.Token("string"),
					b => b.Token("sqstring"),
					b => b.Rule("array"),
					b => b.Rule("object"),
					b => b.Token("anystr_object")
				);

			builder.CreateRule("pair")
				.Choice(
					b => b.Token("string"),
					b => b.Token("sqstring"),
					b => b.Token("anystr_object")
				)
				.Optional(b => b.Literal(':'))
				.Optional(b => b.Rule("value_object"));

			builder.CreateRule("array")
				.Literal('[')
				.ZeroOrMoreSeparated(
					b => b.Rule("value_array"),
					s => s.OneOrMore(b => b.Literal(',')),
					allowTrailingSeparator: true,
					includeSeparatorsInResult: false
				)
				.ZeroOrMore(b => b.Literal(','))
				.Optional(b => b.Literal(']'))

				.Transform(v =>
				{
					var values = new JsonNode?[v[1].Children.Count];
					int i = 0;
					foreach (var child in v[1].Children)
					{
						var childValue = child.Value;
						values[i++] = childValue as JsonNode ?? JsonValue.Create(childValue);
					}
					return new JsonArray(values);
				});

			builder.CreateRule("object")
				.Literal('{')
				.ZeroOrMoreSeparated(
					b => b.Rule("pair"),
					s => s.OneOrMore(b => b.Literal(',')),
					allowTrailingSeparator: true,
					includeSeparatorsInResult: false
				)
				.Optional(b => b.Literal('}'))

				.Transform(v =>
				{
					var result = new JsonObject();
					foreach (var child in v[1].Children)
					{
						var key = child.GetValue<string>(0);
						var value = child.TryGetValue<object>(2);
						result[key] = value as JsonNode ?? JsonValue.Create(value);
					}
					return result;
				});

			builder.CreateMainRule("value")
				.Choice(
					b => b.Token("literal"),
					b => b.Token("number"),
					b => b.Token("string"),
					b => b.Token("sqstring"),
					b => b.Rule("array"),
					b => b.Rule("object")
				)

				.Transform(v =>
				{
					var value = v.GetValue<object>(0);
					return value as JsonNode ?? JsonValue.Create(value);
				});

			_parser = builder.Build();
		}

		/// <summary>
		/// Parses the given string into a JSON node.
		/// </summary>
		/// <param name="input">The string to parse.</param>
		/// <returns>A JSON node or null if the JSON value means null.</returns>
		public static JsonNode? Parse(string input)
		{
			if (input is null)
				return null;
			return _parser.Parse<JsonNode?>(input);
		}

		/// <summary>
		/// Tries to parse the given string into a JSON node.
		/// </summary>
		/// <param name="input">The string to parse.</param>
		/// <param name="result">The parsed JSON node or null if the JSON value means null.</param>
		/// <returns>True if the parsing succeeds; otherwise, false.</returns>
		public static bool TryParse(string input, out JsonNode? result)
		{
			if (input is null)
			{
				result = null;
				return false;
			}
			return _parser.TryParse(input, out result);
		}
	}
}