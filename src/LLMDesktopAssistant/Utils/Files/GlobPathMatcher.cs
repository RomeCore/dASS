using LLMDesktopAssistant.Utils.Patterns;

namespace LLMDesktopAssistant.Utils.Files
{
	/// <summary>
	/// Matches one known path against a filesystem pattern without walking anything. The prefix rule is
	/// the one <see cref="GlobSearch"/> uses - the leading literal segments become the root - and the
	/// remaining segments are matched as a list, so <c>**</c>, the prefix shorthand and the segment
	/// semantics agree with a walk instead of being a second string matcher.
	/// </summary>
	public static class GlobPathMatcher
	{
		/// <summary>
		/// The options a filesystem pattern is compiled with: case-insensitive where the file system is
		/// (Windows), ordinal elsewhere.
		/// </summary>
		public static GlobOptions DefaultOptions => GlobCase.PlatformDefault;

		/// <summary>
		/// True when <paramref name="path"/> names an entry the pattern matches.
		/// </summary>
		/// <param name="pattern">The compiled pattern, with its prefix intact.</param>
		/// <param name="path">The path to test: absolute, or relative to <paramref name="baseDirectory"/>.</param>
		/// <param name="baseDirectory">
		/// The absolute directory a relative pattern and the relative path are resolved against. Must be
		/// absolute: resolving it against the process directory is never what a caller means.
		/// </param>
		public static bool Matches(GlobPattern pattern, string path, string baseDirectory)
		{
			ArgumentNullException.ThrowIfNull(pattern);
			ArgumentNullException.ThrowIfNull(path);
			ArgumentNullException.ThrowIfNull(baseDirectory);

			if (!Path.IsPathRooted(baseDirectory))
				throw new ArgumentException("The base directory must be absolute.", nameof(baseDirectory));

			var start = Path.GetFullPath(baseDirectory);
			var root = pattern.Prefix.Count == 0
				? start
				: Path.GetFullPath(Path.Combine(start, string.Join('/', pattern.Prefix)));

			var target = Path.GetFullPath(Path.Combine(start, path));
			var relative = Path.GetRelativePath(root, target);

			// A '..' step means the entry lives outside the root the pattern resolves to.
			if (relative == ".."
				|| relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
				|| relative.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal))
				return false;

			string[] segments = relative == "."
				? []
				: relative.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
					StringSplitOptions.RemoveEmptyEntries);

			return pattern.Remainder.Matches(segments);
		}

		/// <summary>
		/// True when a path matches the pattern, comparing text only: nothing is resolved against the
		/// file system or the working directory.
		/// </summary>
		/// <param name="pattern">The compiled pattern, with its prefix intact.</param>
		/// <param name="path">The path to test, absolute or relative; both separators are accepted.</param>
		/// <remarks>
		/// Matching starts at the first segment, so a relative pattern only matches relative paths and
		/// an absolute one only absolute paths. '<c>.</c>' and '<c>..</c>' are folded lexically.
		/// </remarks>
		public static bool Matches(GlobPattern pattern, string path)
		{
			ArgumentNullException.ThrowIfNull(pattern);
			ArgumentNullException.ThrowIfNull(path);

			// A rooted pattern names a place on disk, so a relative path can never mean the same place.
			if (pattern.Prefix.Count > 0 && pattern.Prefix[0] == "/" && !IsRooted(path))
				return false;

			var segments = SplitForMatch(path);
			var prefix = MatchablePrefix(pattern.Prefix);

			if (segments.Count < prefix.Count)
				return false;

			var comparison = pattern.Options.HasFlag(GlobOptions.IgnoreCase)
				? StringComparison.OrdinalIgnoreCase
				: StringComparison.Ordinal;

			for (var i = 0; i < prefix.Count; i++)
			{
				if (!string.Equals(segments[i], prefix[i], comparison))
					return false;
			}

			return pattern.Remainder.Matches(
				segments.GetRange(prefix.Count, segments.Count - prefix.Count));
		}

		private static bool IsRooted(string path)
		{
			return path.Length > 0 && (path[0] == '/' || path[0] == '\\');
		}

		/// <summary>
		/// Splits a path into segments, folding '<c>.</c>' away and resolving '<c>..</c>' lexically.
		/// </summary>
		private static List<string> SplitForMatch(string path)
		{
			var parts = path.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
			var segments = new List<string>(parts.Length);

			foreach (var part in parts)
			{
				if (part == ".")
					continue;

				if (part == ".." && segments.Count > 0 && segments[^1] != "..")
					segments.RemoveAt(segments.Count - 1);
				else
					segments.Add(part);
			}

			return segments;
		}

		/// <summary>
		/// The prefix as it appears in a path: the rooted marker and trailing separators do not.
		/// </summary>
		private static List<string> MatchablePrefix(IReadOnlyList<string> prefix)
		{
			var result = new List<string>(prefix.Count);

			foreach (var segment in prefix)
			{
				var trimmed = segment.TrimEnd('/', '\\');

				if (trimmed.Length > 0)
					result.Add(trimmed);
			}

			return result;
		}
	}
}
