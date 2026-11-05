using System.Text;
using System.Text.RegularExpressions;

namespace LLMDesktopAssistant.Utils.Patterns
{
	/// <summary>
	/// A compiled pattern over one flat string: '<c>*</c>' crosses every character, separators are
	/// ordinary characters and '<c>**</c>' is rejected. Used for names, keys, tool ids and MIME
	/// types, where there is no hierarchy to walk.
	/// </summary>
	public sealed class GlobTextPattern
	{
		private readonly Regex _regex;

		private GlobTextPattern(Regex regex, IReadOnlyList<string> alternatives,
			bool hasUnboundedWildcard, bool startsWithLiteralDot)
		{
			_regex = regex;
			Alternatives = alternatives;
			HasUnboundedWildcard = hasUnboundedWildcard;
			StartsWithLiteralDot = startsWithLiteralDot;
		}

		/// <summary>
		/// The expanded alternatives as text, in source order.
		/// </summary>
		public IReadOnlyList<string> Alternatives { get; }

		/// <summary>
		/// True when the pattern contains '<c>*</c>' or '<c>?</c>', so it does not name one exact
		/// string. Used by the ignore policy, where a wildcard must not override an explicit name.
		/// </summary>
		public bool HasUnboundedWildcard { get; }

		/// <summary>
		/// True when some alternative starts with a literal dot, which is how a pattern asks for
		/// hidden entries.
		/// </summary>
		public bool StartsWithLiteralDot { get; }

		/// <summary>
		/// Compiles the pattern, or throws when the options do not fit a flat pattern.
		/// </summary>
		/// <param name="pattern">The pattern text to compile.</param>
		/// <param name="options">
		/// May carry <see cref="GlobOptions.IgnoreCase"/> and <see cref="GlobOptions.ArgumentTail"/>.
		/// </param>
		/// <returns>The compiled pattern.</returns>
		public static GlobTextPattern Parse(string pattern, GlobOptions options = GlobOptions.None)
		{
			EnsureSupported(options, pattern);

			if (string.IsNullOrWhiteSpace(pattern))
				throw new GlobException(GlobError.Empty, pattern, 0, "Empty glob pattern.");

			if (pattern.Length > GlobLimits.MaxPatternLength)
				throw new GlobException(GlobError.TooLong, pattern, 0,
					$"Glob pattern is too long ({pattern.Length} > {GlobLimits.MaxPatternLength}).");

			return FromUnits(GlobPatternParser.Parse(pattern), pattern, options);
		}

		/// <summary>
		/// Compiles an already parsed unit list. Used by the path compiler, which hands over one
		/// segment at a time, and by anything else that already has units in hand.
		/// </summary>
		/// <param name="units">The units to compile.</param>
		/// <param name="pattern">The source pattern, used in error messages.</param>
		/// <param name="options">
		/// May carry <see cref="GlobOptions.IgnoreCase"/> and <see cref="GlobOptions.ArgumentTail"/>.
		/// </param>
		/// <returns>The compiled pattern.</returns>
		public static GlobTextPattern FromUnits(IReadOnlyList<GlobUnit> units, string pattern,
			GlobOptions options = GlobOptions.None)
		{
			EnsureSupported(options, pattern);

			var alternatives = GlobExpander.Expand(units, pattern);
			var argumentTail = options.HasFlag(GlobOptions.ArgumentTail);

			var sources = new List<string>(alternatives.Count);
			var texts = new List<string>(alternatives.Count);
			var hasUnboundedWildcard = false;
			var startsWithLiteralDot = false;

			foreach (var alternative in alternatives)
			{
				if (alternative.Count > GlobLimits.MaxUnits)
					throw new GlobException(GlobError.TooManyUnits, pattern, 0,
						$"Glob pattern has more than {GlobLimits.MaxUnits} units.");

				sources.Add(CompileAlternative(alternative, pattern, argumentTail, out var text));
				texts.Add(text);

				hasUnboundedWildcard |= alternative.Any(unit => unit is GlobAnyChars or GlobAnyChar);
				startsWithLiteralDot |= alternative.Count > 0
					&& alternative[0] is GlobLiteral { Value: '.' };
			}

			// Singleline on purpose: a flat pattern matches prose (a command line, a tool id), not a path
			// segment, so '*' and '?' must cross a line break - otherwise embedding one in a quoted
			// argument would slip past a rule like 'rm -rf *'.
			var comparison = RegexOptions.CultureInvariant | RegexOptions.Singleline;
			if (options.HasFlag(GlobOptions.IgnoreCase))
				comparison |= RegexOptions.IgnoreCase;

			var regex = new Regex(
				"\\A(?:" + string.Join("|", sources.Select(source => $"(?:{source})")) + ")\\z",
				comparison);

			return new GlobTextPattern(regex, texts, hasUnboundedWildcard, startsWithLiteralDot);
		}

		/// <summary>
		/// True when the text matches any alternative.
		/// </summary>
		/// <param name="text">The text to match.</param>
		public bool Matches(string text)
		{
			return _regex.IsMatch(text);
		}

		private static void EnsureSupported(GlobOptions options, string pattern)
		{
			const GlobOptions allowed = GlobOptions.IgnoreCase | GlobOptions.ArgumentTail;
			var rejected = options & ~allowed;

			if (rejected != GlobOptions.None)
				throw new GlobException(GlobError.OptionNotSupported, pattern, 0,
					$"GlobTextPattern does not support the option {rejected}.");
		}

		private static string CompileAlternative(IReadOnlyList<GlobUnit> units, string pattern,
			bool argumentTail, out string text)
		{
			var regex = new StringBuilder();
			var rendered = new StringBuilder();
			var tailIndex = FindArgumentTail(units, pattern, argumentTail);

			for (var i = 0; i < units.Count; i++)
			{
				if (i == tailIndex)
				{
					regex.Append("(?:\\z|[ \\t].*)");
					rendered.Append(":*");
					i++;
					continue;
				}

				switch (units[i])
				{
					case GlobLiteral literal:
						regex.Append(Regex.Escape(literal.Value.ToString()));
						rendered.Append(literal.Value);
						break;
					case GlobAnyChars:
						regex.Append(".*");
						rendered.Append('*');
						break;
					case GlobAnyChar:
						regex.Append('.');
						rendered.Append('?');
						break;
					case GlobSeparator:
						regex.Append("[/\\\\]");
						rendered.Append('/');
						break;
					case GlobClass characterClass:
						regex.Append(GlobClassCompiler.Compile(characterClass, pattern));
						rendered.Append('[').Append(characterClass.Body).Append(']');
						break;
					case GlobDoubleStar:
						throw new GlobException(GlobError.DoubleStarUnsupported, pattern,
							units[i].Position, "'**' is not supported for name patterns; use '*'.");
					default:
						throw new GlobException(GlobError.InvalidSyntax, pattern, units[i].Position,
							"A brace group reached compilation unexpanded.");
				}
			}

			text = rendered.ToString();
			return regex.ToString();
		}

		private static int FindArgumentTail(IReadOnlyList<GlobUnit> units, string pattern,
			bool argumentTail)
		{
			if (!argumentTail)
				return -1;

			var found = -1;

			for (var i = 0; i + 1 < units.Count; i++)
			{
				if (units[i] is GlobLiteral { Value: ':' } && units[i + 1] is GlobAnyChars)
					found = i;
			}

			if (found < 0)
				return -1;

			if (found + 2 != units.Count)
				throw new GlobException(GlobError.ArgumentTailNotTerminal, pattern,
					units[found].Position, "':*' is only valid at the end of a pattern.");

			return found;
		}
	}
}
