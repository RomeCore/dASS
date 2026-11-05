namespace LLMDesktopAssistant.Tools.Specifiers
{
	/// <summary>
	/// The syntactic constructs a rule is made of. Plain literals have no token: only what a reader has to
	/// tell apart from prose is coloured.
	/// </summary>
	public enum SpecifierTokenKind
	{
		/// <summary>
		/// <c>||</c> or <c>&amp;&amp;</c>.
		/// </summary>
		Separator,

		/// <summary>
		/// A parameter reference, <c>name:</c>, for a name the tool declares.
		/// </summary>
		Parameter,

		/// <summary>
		/// <c>*</c>, <c>?</c> or <c>**</c>.
		/// </summary>
		Wildcard,

		/// <summary>
		/// A character class, brackets included.
		/// </summary>
		Class,

		/// <summary>
		/// An opening or closing brace of an alternatives group.
		/// </summary>
		Brace,
	}

	/// <summary>
	/// One construct of a rule, in character coordinates of the raw text.
	/// </summary>
	/// <param name="Kind">The construct.</param>
	/// <param name="Start">The zero-based index of the construct's first character.</param>
	/// <param name="Length">The length of the construct.</param>
	public readonly record struct SpecifierToken(SpecifierTokenKind Kind, int Start, int Length);

	/// <summary>
	/// Splits a rule into the constructs worth colouring. It is deliberately looser than the grammar: a
	/// highlighter must not fail on a half-written rule, so an unterminated class or a stray brace simply
	/// stays plain text.
	/// </summary>
	public static class SpecifierPatternTokenizer
	{
		/// <summary>
		/// Tokenizes a rule.
		/// </summary>
		/// <param name="pattern">The rule text.</param>
		/// <param name="parameterNames">
		/// The names the tool declares. A name that is not in this set stays part of a literal, exactly as
		/// the parser treats it.
		/// </param>
		/// <returns>The constructs in source order.</returns>
		public static IReadOnlyList<SpecifierToken> Tokenize(string pattern,
			IReadOnlyCollection<string>? parameterNames = null)
		{
			var tokens = new List<SpecifierToken>();
			if (string.IsNullOrEmpty(pattern))
				return tokens;

			var names = parameterNames is null ? null : new HashSet<string>(parameterNames, StringComparer.Ordinal);
			var braceDepth = 0;

			for (var index = 0; index < pattern.Length;)
			{
				var current = pattern[index];

				// '\||' and '\&&' are the grammar's escapes: the escaped pair is a plain literal.
				if (current == '\\' && index + 2 < pattern.Length && IsSeparatorPair(pattern, index + 1))
				{
					index += 3;
					continue;
				}

				if (IsSeparatorPair(pattern, index))
				{
					tokens.Add(new SpecifierToken(SpecifierTokenKind.Separator, index, 2));
					index += 2;
					continue;
				}

				if (char.IsLetter(current) || current == '_')
				{
					var start = index;
					index++;

					while (index < pattern.Length && (char.IsLetterOrDigit(pattern[index]) || pattern[index] == '_'))
						index++;

					if (index < pattern.Length && pattern[index] == ':' &&
						(names is null || names.Contains(pattern[start..index])))
					{
						tokens.Add(new SpecifierToken(SpecifierTokenKind.Parameter, start, index - start + 1));
					}

					continue;
				}

				if (current is '*' or '?')
				{
					var length = current == '*' && index + 1 < pattern.Length && pattern[index + 1] == '*' ? 2 : 1;
					tokens.Add(new SpecifierToken(SpecifierTokenKind.Wildcard, index, length));
					index += length;
					continue;
				}

				if (current == '[')
				{
					var end = FindClassEnd(pattern, index);
					if (end < 0)
					{
						index++;
						continue;
					}

					tokens.Add(new SpecifierToken(SpecifierTokenKind.Class, index, end - index + 1));
					index = end + 1;
					continue;
				}

				if (current == '{')
				{
					tokens.Add(new SpecifierToken(SpecifierTokenKind.Brace, index, 1));
					braceDepth++;
					index++;
					continue;
				}

				if (current == '}' && braceDepth > 0)
				{
					tokens.Add(new SpecifierToken(SpecifierTokenKind.Brace, index, 1));
					braceDepth--;
					index++;
					continue;
				}

				index++;
			}

			return tokens;
		}

		/// <summary>
		/// Finds the closing bracket of a class, or <c>-1</c> when the class never closes.
		/// </summary>
		private static int FindClassEnd(string pattern, int start)
		{
			var index = start + 1;

			// The grammar allows a negation marker and a leading ']' as the first element.
			if (index < pattern.Length && pattern[index] is '!' or '^')
				index++;
			if (index < pattern.Length && pattern[index] == ']')
				index++;

			for (; index < pattern.Length; index++)
			{
				if (pattern[index] == ']')
					return index;
			}

			return -1;
		}

		/// <summary>
		/// Whether a separator pair, <c>||</c> or <c>&amp;&amp;</c>, starts at the index.
		/// </summary>
		private static bool IsSeparatorPair(string pattern, int index) =>
			index + 1 < pattern.Length &&
			pattern[index] == pattern[index + 1] &&
			pattern[index] is '|' or '&';
	}
}
