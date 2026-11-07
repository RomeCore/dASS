namespace LLMDesktopAssistant.Utils.Patterns
{
	/// <summary>
	/// A compiled set of flat patterns, matched as a union. Compiled once and matched many times,
	/// which is the shape every caller wants: skip lists, ignore arguments, allow and deny lists.
	/// </summary>
	public sealed class GlobTextSet
	{
		private readonly GlobTextPattern[] _patterns;

		private GlobTextSet(GlobTextPattern[] patterns)
		{
			_patterns = patterns;
		}

		/// <summary>
		/// A set that matches nothing.
		/// </summary>
		public static GlobTextSet Empty { get; } = new([]);

		/// <summary>
		/// The names a walk skips by default: version control, IDE state, caches and build output.
		/// '<c>.*</c>' covers every hidden entry, which is why there is no separate "show hidden"
		/// switch anywhere.
		/// </summary>
		/// <remarks>
		/// Compiled with <see cref="GlobCase.PlatformDefault"/>, like every other scanning pattern,
		/// so on Windows 'Bin' is skipped as readily as 'bin'.
		/// </remarks>
		public static GlobTextSet DefaultIgnore { get; } = ParseNames([
			".*", "node_modules", "__pycache__", "bin", "obj", "TestResults",
		], GlobCase.PlatformDefault);

		/// <summary>
		/// Compiles patterns meant to match a single entry name. A separator in any of them is
		/// rejected, because a name never contains one and the pattern would silently match nothing.
		/// </summary>
		/// <param name="patterns">The patterns to compile.</param>
		/// <param name="options">May carry <see cref="GlobOptions.IgnoreCase"/>.</param>
		/// <returns>The compiled set.</returns>
		public static GlobTextSet ParseNames(IEnumerable<string> patterns,
			GlobOptions options = GlobOptions.None)
		{
			var compiled = new List<GlobTextPattern>();

			foreach (var pattern in patterns)
			{
				EnsureNoSeparator(GlobPatternParser.Parse(pattern), pattern);
				compiled.Add(GlobTextPattern.Parse(pattern, options));
			}

			return new GlobTextSet([.. compiled]);
		}

		/// <summary>
		/// True when any pattern of the set matches the text.
		/// </summary>
		/// <param name="text">The text to match.</param>
		public bool Matches(string text)
		{
			foreach (var pattern in _patterns)
			{
				if (pattern.Matches(text))
					return true;
			}

			return false;
		}

		private static void EnsureNoSeparator(IReadOnlyList<GlobUnit> units, string pattern)
		{
			foreach (var unit in units)
			{
				if (unit is GlobSeparator)
					throw new GlobException(GlobError.SegmentPatternExpected, pattern, unit.Position,
						$"Ignore patterns match a single entry name; '{pattern}' contains a separator.");

				if (unit is GlobBrace brace)
				{
					foreach (var alternative in brace.Alternatives)
						EnsureNoSeparator(alternative, pattern);
				}
			}
		}
	}
}
