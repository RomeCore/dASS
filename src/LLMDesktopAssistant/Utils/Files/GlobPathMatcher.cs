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
		public static GlobOptions DefaultOptions => OperatingSystem.IsWindows()
			? GlobOptions.IgnoreCase
			: GlobOptions.None;

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
	}
}
