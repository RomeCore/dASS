namespace LLMDesktopAssistant.Utils.Files
{
	/// <summary>
	/// One entry accepted by a walk. Carries identity only - sizes and line counts are read by the
	/// caller, and only for the entries that survive the limit.
	/// </summary>
	public sealed record GlobEntry
	{
		/// <summary>
		/// The absolute path of the entry.
		/// </summary>
		public required string FullPath { get; init; }

		/// <summary>
		/// The path relative to the search root, with the platform separator.
		/// </summary>
		public required string RelativeToRoot { get; init; }

		/// <summary>
		/// The last segment of the path.
		/// </summary>
		public required string Name { get; init; }

		/// <summary>
		/// True when the entry is a directory.
		/// </summary>
		public bool IsDirectory { get; init; }
	}
}
