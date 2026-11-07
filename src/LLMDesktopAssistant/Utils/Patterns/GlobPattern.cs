using System.Text;

namespace LLMDesktopAssistant.Utils.Patterns
{
	/// <summary>
	/// A compiled pattern over a hierarchy of segments: a leading prefix that resolves without
	/// matching, and the alternatives that still have to be matched inside it. Pure - it never
	/// touches the file system, so it cannot resolve the prefix against a working directory on its
	/// own; that is the caller's single job.
	/// </summary>
	public sealed class GlobPattern
	{
		private readonly GlobAlternative[] _alternatives;

		private GlobPattern(IReadOnlyList<string> prefix, GlobAlternative[] alternatives,
			bool requiresDirectory, IReadOnlyList<string> alternativeTexts, GlobOptions options)
		{
			Prefix = prefix;
			_alternatives = alternatives;
			RequiresDirectory = requiresDirectory;
			AlternativeTexts = alternativeTexts;
			Options = options;
			Remainder = prefix.Count == 0
				? this
				: new GlobPattern([], alternatives, requiresDirectory, alternativeTexts, options);
		}

		/// <summary>
		/// The leading segments that resolve without matching: literals and path operators
		/// ('<c>.</c>', '<c>..</c>', '<c>/</c>', '<c>X:</c>'). Empty when prefix extraction is
		/// disabled.
		/// </summary>
		public IReadOnlyList<string> Prefix { get; }

		/// <summary>
		/// One entry per brace alternative, with the prefix already removed.
		/// </summary>
		public IReadOnlyList<GlobAlternative> Alternatives => _alternatives;

		/// <summary>
		/// True when the pattern ended with a separator and therefore only denotes a directory.
		/// </summary>
		public bool RequiresDirectory { get; }

		/// <summary>
		/// The expanded alternatives as text, in source order, for diagnostics and messages.
		/// </summary>
		public IReadOnlyList<string> AlternativeTexts { get; }

		/// <summary>
		/// The options the pattern was parsed with.
		/// </summary>
		/// <remarks>
		/// The case rule has to travel with the pattern: the alternatives are compiled with it, and a
		/// caller comparing <see cref="Prefix"/> segments as text would otherwise pick a different one.
		/// </remarks>
		public GlobOptions Options { get; }

		/// <summary>
		/// The same pattern with <see cref="Prefix"/> removed. Its own prefix is always empty.
		/// </summary>
		public GlobPattern Remainder { get; }

		/// <summary>
		/// True when no alternative has segments left to match, so only the search root itself can
		/// match. The walker reports that root instead of descending into it.
		/// </summary>
		public bool IsEmpty => _alternatives.All(alternative => alternative.Segments.Count == 0);

		/// <summary>
		/// Compiles the pattern, or throws when the dialect does not cover it.
		/// </summary>
		/// <param name="pattern">The pattern text to compile.</param>
		/// <param name="options">
		/// May carry <see cref="GlobOptions.IgnoreCase"/> and <see cref="GlobOptions.NoPrefix"/>.
		/// </param>
		/// <returns>The compiled pattern.</returns>
		public static GlobPattern Parse(string pattern, GlobOptions options = GlobOptions.None)
		{
			if (options.HasFlag(GlobOptions.ArgumentTail))
				throw new GlobException(GlobError.OptionNotSupported, pattern, 0,
					"GlobOptions.ArgumentTail is only valid for command patterns.");

			if (string.IsNullOrWhiteSpace(pattern))
				throw new GlobException(GlobError.Empty, pattern, 0, "Empty glob pattern.");

			if (pattern.Length > GlobLimits.MaxPatternLength)
				throw new GlobException(GlobError.TooLong, pattern, 0,
					$"Glob pattern is too long ({pattern.Length} > {GlobLimits.MaxPatternLength}).");

			var units = GlobPatternParser.Parse(pattern);
			var requiresDirectory = units.Count > 0 && units[^1] is GlobSeparator;
			var expanded = GlobExpander.Expand(units, pattern);
			var noPrefix = options.HasFlag(GlobOptions.NoPrefix);

			var rooted = expanded.Count > 0 && expanded[0].Count > 0 && expanded[0][0] is GlobSeparator;

			if (expanded.Any(alternative =>
				(alternative.Count > 0 && alternative[0] is GlobSeparator) != rooted))
				throw new GlobException(GlobError.ConflictingRoot, pattern, 0,
					"Brace alternatives disagree on whether the pattern is rooted.");

			var segmentsByAlternative = new List<List<List<GlobUnit>>>(expanded.Count);
			var candidates = new List<List<string>>(expanded.Count);
			var texts = new List<string>(expanded.Count);

			foreach (var alternative in expanded)
			{
				if (alternative.Count > GlobLimits.MaxUnits)
					throw new GlobException(GlobError.TooManyUnits, pattern, 0,
						$"Glob pattern has more than {GlobLimits.MaxUnits} units.");

				var body = rooted ? alternative.GetRange(1, alternative.Count - 1) : alternative;

				if (requiresDirectory && body.Count > 0 && body[^1] is GlobSeparator)
					body = body.GetRange(0, body.Count - 1);

				var segments = SplitSegments(body);

				if (noPrefix)
					EnsureNoParentSegment(segments, pattern);

				segmentsByAlternative.Add(segments);
				candidates.Add(noPrefix ? [] : ExtractCandidates(segments, rooted, requiresDirectory, pattern));
				texts.Add(GlobExpander.Render(alternative));
			}

			var prefix = noPrefix ? [] : NormalizePrefix(CommonPrefix(candidates));
			var skipped = noPrefix ? 0 : prefix.Count - (rooted ? 1 : 0);

			var alternatives = new List<GlobAlternative>(segmentsByAlternative.Count);
			foreach (var segments in segmentsByAlternative)
				alternatives.Add(CompileAlternative(segments.Skip(skipped), pattern, options));

			return new GlobPattern(prefix, [.. alternatives], requiresDirectory, texts, options);
		}

		/// <summary>
		/// True when the segments are a complete match.
		/// </summary>
		/// <param name="segments">The path segments to match, relative to the search root.</param>
		public bool Matches(IReadOnlyList<string> segments)
		{
			foreach (var alternative in _alternatives)
			{
				var row = Start(alternative);
				foreach (var name in segments)
					row = Step(alternative, row, name);

				if (IsAccepting(alternative, row))
					return true;
			}

			return false;
		}

		/// <summary>
		/// True when the prefix can still be extended into a match. A walker uses it to stop
		/// descending, and the guarantee is exact: a false result never hides a match.
		/// </summary>
		/// <param name="segments">The path segments consumed so far, relative to the search root.</param>
		public bool IsPrefixPossible(IReadOnlyList<string> segments)
		{
			foreach (var alternative in _alternatives)
			{
				var row = Start(alternative);
				foreach (var name in segments)
					row = Step(alternative, row, name);

				if (row.Any(live => live))
					return true;
			}

			return false;
		}

		/// <summary>
		/// True when the pattern constrains this position to this exact name, without an unbounded
		/// wildcard or a leading-dot exception. The ignore policy must not skip such an entry.
		/// </summary>
		/// <param name="prefix">The path segments consumed so far, relative to the search root.</param>
		/// <param name="name">The entry name the policy is about to skip.</param>
		public bool IsExplicitName(IReadOnlyList<string> prefix, string name)
		{
			foreach (var alternative in _alternatives)
			{
				var row = Start(alternative);
				foreach (var segment in prefix)
					row = Step(alternative, row, segment);

				for (var s = 0; s < alternative.Segments.Count; s++)
				{
					if (!row[s])
						continue;

					var segment = alternative.Segments[s];
					if (segment.Kind != GlobSegmentKind.Pattern)
						continue;

					var pattern = segment.Pattern!;
					if (pattern.HasUnboundedWildcard && !pattern.StartsWithLiteralDot)
						continue;

					if (pattern.Matches(name))
						return true;
				}
			}

			return false;
		}

		private static List<List<GlobUnit>> SplitSegments(List<GlobUnit> units)
		{
			var segments = new List<List<GlobUnit>>();
			var current = new List<GlobUnit>();

			foreach (var unit in units)
			{
				if (unit is GlobSeparator)
				{
					segments.Add(current);
					current = new List<GlobUnit>();
					continue;
				}

				current.Add(unit);
			}

			segments.Add(current);
			return segments.Where(segment => segment.Count > 0).ToList();
		}

		/// <summary>
		/// Takes the leading segments that resolve without matching. The last segment always stays
		/// in the remainder, so a fully literal pattern still names the entry the walk has to find
		/// rather than turning the search root into that entry. A lone segment is the exception when
		/// it names a location instead of an entry: a drive, a parent reference or a trailing separator.
		/// </summary>
		private static List<string> ExtractCandidates(List<List<GlobUnit>> segments, bool rooted,
			bool requiresDirectory, string pattern)
		{
			var candidates = new List<string>();
			if (rooted)
				candidates.Add("/");

			int limit;
			if (segments.Count > 1)
			{
				limit = segments.Count - 1;
			}
			else
			{
				var isLocation = requiresDirectory
					|| (TryGetLiteral(segments[0], out var only) && IsLocationSegment(only));
				limit = isLocation ? 1 : 0;
			}

			// A drive-relative segment such as 'C:foo' is a path error wherever it appears, so it is
			// rejected before extraction decides whether the segment becomes the root.
			foreach (var segment in segments)
			{
				if (!TryGetLiteral(segment, out var literal))
					break;

				if (literal.Length > 2 && literal[1] == ':' && char.IsAsciiLetter(literal[0]))
					throw new GlobException(GlobError.DriveRelativePattern, pattern,
						segment[0].Position,
						$"A drive prefix must be followed by a separator, not by '{literal[2..]}'.");
			}

			var taken = 0;

			foreach (var segment in segments)
			{
				if (taken >= limit || !TryGetLiteral(segment, out var literal))
					break;

				candidates.Add(literal);
				taken++;
			}

			return candidates;
		}

		private static bool TryGetLiteral(List<GlobUnit> segment, out string literal)
		{
			var builder = new StringBuilder();

			foreach (var unit in segment)
			{
				if (unit is not GlobLiteral unit1)
				{
					literal = string.Empty;
					return false;
				}

				builder.Append(unit1.Value);
			}

			literal = builder.ToString();
			return true;
		}

		/// <summary>
		/// A lone segment that names a location rather than an entry: a drive specifier such as
		/// '<c>C:</c>', the current directory or a parent reference.
		/// </summary>
		private static bool IsLocationSegment(string literal)
		{
			if (literal is "." or "..")
				return true;

			return literal.Length == 2 && literal[1] == ':' && char.IsAsciiLetter(literal[0]);
		}

		/// <summary>
		/// The longest leading run of identical candidate segments across all alternatives.
		/// </summary>
		private static List<string> CommonPrefix(List<List<string>> candidates)
		{
			var common = new List<string>();

			if (candidates.Count == 0)
				return common;

			for (var i = 0; ; i++)
			{
				if (candidates[0].Count <= i)
					break;

				var value = candidates[0][i];

				if (candidates.Any(candidate => candidate.Count <= i
					|| !string.Equals(candidate[i], value, StringComparison.Ordinal)))
					break;

				common.Add(value);
			}

			return common;
		}

		/// <summary>
		/// A drive letter on its own is drive-relative in path arithmetic, so it is rewritten to a
		/// drive root when it ends the prefix.
		/// </summary>
		private static List<string> NormalizePrefix(List<string> prefix)
		{
			if (prefix.Count > 0)
			{
				var last = prefix[^1];

				if (last.Length == 2 && last[1] == ':' && char.IsAsciiLetter(last[0]))
					prefix[^1] = last + "/";
			}

			return prefix;
		}

		private static void EnsureNoParentSegment(List<List<GlobUnit>> segments, string pattern)
		{
			foreach (var segment in segments)
			{
				if (segment.Count == 2
					&& segment[0] is GlobLiteral { Value: '.' }
					&& segment[1] is GlobLiteral { Value: '.' })
					throw new GlobException(GlobError.ParentSegment, pattern, segment[0].Position,
						"Patterns may not contain '..' without prefix extraction.");
			}
		}

		private static GlobAlternative CompileAlternative(IEnumerable<List<GlobUnit>> segments,
			string pattern, GlobOptions options)
		{
			var compiled = new List<GlobSegment>();

			foreach (var segment in segments)
			{
				var stars = segment.Where(unit => unit is GlobDoubleStar).ToList();

				if (stars.Count > 1 || (stars.Count == 1 && segment.Count > 1))
					throw new GlobException(GlobError.DoubleStarNotWholeSegment, pattern,
						stars[0].Position,
						"'**' must occupy a whole path segment. Use '*' inside a segment.");

				if (stars.Count == 1)
					compiled.Add(new GlobSegment(GlobSegmentKind.DoubleStar, null));
				else
					compiled.Add(new GlobSegment(GlobSegmentKind.Pattern,
						GlobTextPattern.FromUnits(segment, pattern, options & GlobOptions.IgnoreCase)));
			}

			return new GlobAlternative(compiled);
		}

		private static bool[] Start(GlobAlternative alternative)
		{
			var row = new bool[alternative.Segments.Count + 1];
			row[0] = true;
			Closure(alternative, row);
			return row;
		}

		private static bool[] Step(GlobAlternative alternative, bool[] row, string name)
		{
			var next = new bool[row.Length];

			for (var s = 0; s < alternative.Segments.Count; s++)
			{
				if (!row[s])
					continue;

				var segment = alternative.Segments[s];

				if (segment.Kind == GlobSegmentKind.DoubleStar)
				{
					next[s] = true;
					next[s + 1] = true;
				}
				else if (segment.Pattern!.Matches(name))
					next[s + 1] = true;
			}

			Closure(alternative, next);
			return next;
		}

		/// <summary>
		/// A '<c>**</c>' may match zero segments, so it is live both before and after itself.
		/// </summary>
		private static void Closure(GlobAlternative alternative, bool[] row)
		{
			for (var s = 0; s < alternative.Segments.Count; s++)
			{
				if (row[s] && alternative.Segments[s].Kind == GlobSegmentKind.DoubleStar)
					row[s + 1] = true;
			}
		}

		private static bool IsAccepting(GlobAlternative alternative, bool[] row)
		{
			for (var s = 0; s < row.Length; s++)
			{
				if (row[s] && alternative.IsAcceptingAt(s))
					return true;
			}

			return false;
		}
	}
}
