namespace LLMDesktopAssistant.Utils.Patterns
{
	/// <summary>
	/// The reason a pattern was rejected. Every value is a loud failure: a pattern the engine
	/// cannot honour never degrades into "no matches".
	/// </summary>
	public enum GlobError
	{
		/// <summary>
		/// The pattern is empty or consists of whitespace only.
		/// </summary>
		Empty,

		/// <summary>
		/// The pattern is longer than the supported maximum.
		/// </summary>
		TooLong,

		/// <summary>
		/// Brace expansion produced more alternatives than the supported maximum.
		/// </summary>
		TooManyAlternatives,

		/// <summary>
		/// One alternative has more units than the supported maximum.
		/// </summary>
		TooManyUnits,

		/// <summary>
		/// The pattern did not parse. Reachable only for input the grammar cannot describe at all.
		/// </summary>
		InvalidSyntax,

		/// <summary>
		/// '<c>..</c>' was used while prefix extraction was disabled.
		/// </summary>
		ParentSegment,

		/// <summary>
		/// A drive letter was followed by something other than a separator.
		/// </summary>
		DriveRelativePattern,

		/// <summary>
		/// A rooted pattern prefix disagrees with the starting path it was combined with.
		/// </summary>
		ConflictingRoot,

		/// <summary>
		/// '<c>**</c>' appeared inside a segment instead of occupying it alone.
		/// </summary>
		DoubleStarNotWholeSegment,

		/// <summary>
		/// '<c>**</c>' was used in a flat pattern, where it has no meaning.
		/// </summary>
		DoubleStarUnsupported,

		/// <summary>
		/// '<c>:*</c>' appeared anywhere but at the end of a command pattern.
		/// </summary>
		ArgumentTailNotTerminal,

		/// <summary>
		/// An option was passed to a pattern kind that cannot honour it.
		/// </summary>
		OptionNotSupported,

		/// <summary>
		/// A character class declares a range whose upper bound is below its lower bound.
		/// </summary>
		InvalidClassRange,

		/// <summary>
		/// The pattern starts with '<c>!</c>', which is reserved for a negation the dialect does
		/// not have.
		/// </summary>
		NegationUnsupported,

		/// <summary>
		/// A pattern meant to match one entry name contains a separator.
		/// </summary>
		SegmentPatternExpected,
	}
}
