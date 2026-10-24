namespace LLMDesktopAssistant.Scripting
{
	/// <summary>
	/// How the values of a scriptable addon reach its script.
	/// </summary>
	public enum ScriptInputMode
	{
		/// <summary>
		/// Native structure where the language can represent it, a JSON string otherwise.
		/// </summary>
		Auto = 0,

		/// <summary>
		/// A JSON string the script parses itself.
		/// </summary>
		Json,

		/// <summary>
		/// The language's own structure — a dict/list literal, an object.
		/// </summary>
		Structured,
	}
}
