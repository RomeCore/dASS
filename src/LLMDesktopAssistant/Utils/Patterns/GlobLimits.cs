namespace LLMDesktopAssistant.Utils.Patterns
{
	/// <summary>
	/// Hard limits applied to every pattern, so that a pattern written by a model cannot turn
	/// into an unbounded amount of work or memory.
	/// </summary>
	public static class GlobLimits
	{
		/// <summary>
		/// The maximum length of a pattern in characters.
		/// </summary>
		public const int MaxPatternLength = 1024;

		/// <summary>
		/// The maximum number of units in one alternative.
		/// </summary>
		public const int MaxUnits = 256;
	}
}
