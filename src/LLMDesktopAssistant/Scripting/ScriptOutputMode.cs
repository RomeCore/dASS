namespace LLMDesktopAssistant.Scripting
{
	/// <summary>
	/// Where a scriptable addon's structured result is taken from.
	/// </summary>
	public enum ScriptOutputMode
	{
		/// <summary>
		/// No structured result; the raw output is the result.
		/// </summary>
		None = 0,

		/// <summary>
		/// The whole standard output must parse as JSON.
		/// </summary>
		Whole,

		/// <summary>
		/// The capture between the pattern's markers must parse as JSON.
		/// </summary>
		Pattern,
	}
}
