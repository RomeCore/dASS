namespace LLMDesktopAssistant.Utils.Patterns
{
	/// <summary>
	/// One brace alternative: a chain of segment matchers that a path is consumed against.
	/// </summary>
	public sealed class GlobAlternative
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="GlobAlternative"/> class.
		/// </summary>
		/// <param name="segments">The segments, in order.</param>
		public GlobAlternative(IReadOnlyList<GlobSegment> segments)
		{
			Segments = segments;
		}

		/// <summary>
		/// The segments, in order.
		/// </summary>
		public IReadOnlyList<GlobSegment> Segments { get; }

		/// <summary>
		/// True when everything from this position onwards is '<c>**</c>', so the path may stop
		/// here and still be a match. This is what makes '<c>a/**</c>' match '<c>a</c>'.
		/// </summary>
		/// <param name="position">The number of segments already consumed from this alternative.</param>
		public bool IsAcceptingAt(int position)
		{
			for (var i = position; i < Segments.Count; i++)
			{
				if (Segments[i].Kind != GlobSegmentKind.DoubleStar)
					return false;
			}

			return true;
		}
	}
}
