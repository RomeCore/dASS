namespace LLMDesktopAssistant.Utils.Patterns
{
	/// <summary>
	/// Comparison and interpretation options for a compiled pattern.
	/// </summary>
	[Flags]
	public enum GlobOptions
	{
		/// <summary>
		/// Ordinal comparison: a pattern and its input must agree on case.
		/// </summary>
		None = 0,

		/// <summary>
		/// Case-insensitive comparison, used by default on case-insensitive file systems.
		/// </summary>
		IgnoreCase = 1,

		/// <summary>
		/// Enables the terminal '<c>:*</c>' operator. Rejected by path patterns, which have no
		/// command tail to match.
		/// </summary>
		ArgumentTail = 2,

		/// <summary>
		/// Disables prefix extraction, so '<c>.</c>', '<c>..</c>' and '<c>X:</c>' stay ordinary
		/// segments and the whole pattern is available for matching a segment list as it is.
		/// </summary>
		NoPrefix = 4,
	}
}
