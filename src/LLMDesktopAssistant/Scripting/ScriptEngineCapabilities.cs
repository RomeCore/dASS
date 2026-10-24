namespace LLMDesktopAssistant.Scripting
{
	/// <summary>
	/// What a script language can do beyond running a body. The capability is a fact about the language, so the
	/// frontmatter can ask for something the engine cannot deliver and be refused at load time instead of quietly
	/// receiving something else.
	/// </summary>
	[Flags]
	public enum ScriptEngineCapabilities
	{
		/// <summary>Nothing beyond running a body.</summary>
		None = 0,

		/// <summary>
		/// The engine can pass a value to the script as its native structure (a dict/list literal, an object).
		/// Without it only a JSON string is possible.
		/// </summary>
		StructuredInput = 1,

		/// <summary>The engine can run a script in an interactive terminal (PTY) instead of a managed process.</summary>
		Terminal = 2,

		/// <summary>The engine can report a structured result taken from the script's output.</summary>
		StructuredOutput = 4,
	}
}
