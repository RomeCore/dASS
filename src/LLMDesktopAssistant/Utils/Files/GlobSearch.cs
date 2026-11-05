using LLMDesktopAssistant.Utils.Patterns;

namespace LLMDesktopAssistant.Utils.Files
{
	/// <summary>
	/// A resolved search: an absolute root directory and the pattern that still applies inside it.
	/// Creating one never touches the file system and never resolves a relative path - that is the
	/// caller's job, because only the caller knows which working directory the path was meant
	/// against, and only the caller can report a root that does not exist in a useful way.
	/// </summary>
	public sealed class GlobSearch
	{
		private GlobSearch(GlobPattern pattern, string sourcePattern, string startingPath,
			string root)
		{
			Pattern = pattern;
			SourcePattern = sourcePattern;
			StartingPath = startingPath;
			Root = root;
		}

		/// <summary>
		/// The pattern exactly as written, for messages and error text.
		/// </summary>
		public string SourcePattern { get; }

		/// <summary>
		/// The pattern that still has to be matched, relative to <see cref="Root"/>. Its own prefix
		/// is always empty.
		/// </summary>
		public GlobPattern Pattern { get; }

		/// <summary>
		/// The absolute directory the caller started from, before the pattern prefix was applied.
		/// </summary>
		public string StartingPath { get; }

		/// <summary>
		/// The absolute directory the walk starts from: <see cref="StartingPath"/> with the pattern
		/// prefix appended.
		/// </summary>
		public string Root { get; }

		/// <summary>
		/// Resolves the pattern prefix against an absolute starting path.
		/// </summary>
		/// <param name="pattern">The glob pattern as the caller wrote it.</param>
		/// <param name="startingPath">
		/// The directory the prefix is appended to. Must be absolute: a relative path would be
		/// resolved against the process working directory, which is not the directory the caller
		/// means.
		/// </param>
		/// <param name="options">Pattern options; case sensitivity is decided here.</param>
		/// <returns>The resolved search.</returns>
		public static GlobSearch From(string pattern, string startingPath,
			GlobOptions options = GlobOptions.None)
		{
			if (!Path.IsPathRooted(startingPath))
				throw new ArgumentException(
					"The starting path must be absolute; resolving it against a working directory is the caller's job.",
					nameof(startingPath));

			var parsed = GlobPattern.Parse(pattern, options);
			var start = Path.GetFullPath(startingPath);

			var root = parsed.Prefix.Count == 0
				? start
				: Path.GetFullPath(Path.Combine(start, string.Join('/', parsed.Prefix)));

			return new GlobSearch(parsed.Remainder, pattern, start, root);
		}

		/// <summary>
		/// Returns a copy rooted elsewhere, used after the access policy has resolved the root.
		/// </summary>
		/// <param name="root">The root to use instead.</param>
		public GlobSearch WithRoot(string root)
		{
			var resolved = Path.IsPathRooted(root)
				? Path.GetFullPath(root)
				: Path.GetFullPath(Path.Combine(StartingPath, root));

			return new GlobSearch(Pattern, SourcePattern, StartingPath, resolved);
		}
	}
}
