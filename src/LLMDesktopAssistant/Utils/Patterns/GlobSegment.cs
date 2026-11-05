namespace LLMDesktopAssistant.Utils.Patterns
{
	/// <summary>
	/// What a segment of an alternative does.
	/// </summary>
	public enum GlobSegmentKind
	{
		/// <summary>
		/// Matches exactly one name.
		/// </summary>
		Pattern,

		/// <summary>
		/// Matches zero or more whole segments.
		/// </summary>
		DoubleStar,
	}

	/// <summary>
	/// One segment of an alternative: either a matcher for a single name or the '<c>**</c>'
	/// operator.
	/// </summary>
	/// <param name="Kind">What the segment does.</param>
	/// <param name="Pattern">The matcher, or <see langword="null"/> for a '<c>**</c>' segment.</param>
	public readonly record struct GlobSegment(GlobSegmentKind Kind, GlobTextPattern? Pattern);
}
