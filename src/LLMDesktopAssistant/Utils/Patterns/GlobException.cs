namespace LLMDesktopAssistant.Utils.Patterns
{
	/// <summary>
	/// Thrown when a pattern cannot be compiled. Carries the reason, the offending pattern and
	/// the position inside it, so a tool can report what is wrong instead of returning an empty
	/// result the caller would read as a fact about the file system.
	/// </summary>
	public sealed class GlobException : ArgumentException
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="GlobException"/> class.
		/// </summary>
		/// <param name="error">The reason the pattern was rejected.</param>
		/// <param name="pattern">The pattern as the caller wrote it.</param>
		/// <param name="position">The position inside <paramref name="pattern"/> the error refers to.</param>
		/// <param name="detail">The message shown to the caller.</param>
		public GlobException(GlobError error, string pattern, int position, string detail)
			: base(detail)
		{
			Error = error;
			Pattern = pattern;
			Position = position;
		}

		/// <summary>
		/// The reason the pattern was rejected.
		/// </summary>
		public GlobError Error { get; }

		/// <summary>
		/// The pattern as the caller wrote it, before any expansion or normalization.
		/// </summary>
		public string Pattern { get; }

		/// <summary>
		/// The zero-based position inside <see cref="Pattern"/> the error refers to.
		/// </summary>
		public int Position { get; }
	}
}
