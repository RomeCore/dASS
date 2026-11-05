using LLMDesktopAssistant.Utils.Patterns;

namespace LLMDesktopAssistant.Utils.Files
{
	/// <summary>
	/// What a walk yields and what it skips.
	/// </summary>
	public sealed record GlobWalkOptions
	{
		/// <summary>
		/// Files only, the default for a content search.
		/// </summary>
		public static GlobWalkOptions FilesOnly { get; } = new();

		/// <summary>
		/// Whether files are reported.
		/// </summary>
		public bool Files { get; init; } = true;

		/// <summary>
		/// Whether directories are reported.
		/// </summary>
		public bool Directories { get; init; }

		/// <summary>
		/// Maximum number of reported entries, or 0 for no limit.
		/// </summary>
		public int Limit { get; init; } = 100;

		/// <summary>
		/// Single-segment patterns over entry names. A matching directory is not descended into,
		/// unless the pattern explicitly names it at that position. Replaces the idea of a
		/// "show hidden" flag: '<c>.*</c>' is an ordinary member of the set.
		/// </summary>
		public GlobTextSet Ignore { get; init; } = GlobTextSet.DefaultIgnore;
	}
}
