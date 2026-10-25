namespace LLMDesktopAssistant.Scripting
{
	/// <summary>
	/// Represents different types of script languages that can be used in the application.
	/// </summary>
	public enum ScriptLanguageType
	{
		/// <summary>
		/// Unknown language type. This is the default value for an uninitialized variable or when no valid language is detected.
		/// </summary>
		Unknown,

		// === SHELL SCRIPTS ===

		/// <summary>
		/// Windows Batch (.bat or .cmd) scripting language.
		/// Available only on Windows operating systems.
		/// </summary>
		Batch,

		/// <summary>
		/// PowerShell (.ps1) scripting language.
		/// PowerShell 7 (pwsh) runs on Windows, Linux and macOS; Windows PowerShell 5.1 only on Windows, and it is
		/// preferred only when pwsh is absent.
		/// </summary>
		PowerShell,

		/// <summary>
		/// Bash (.sh) scripting language.
		/// Available on Unix-like operating systems natively.
		/// On Windows, the git's or WSL's bash.exe used.
		/// </summary>
		Bash,

		// === EMBEDDED SCRIPTS ===

		/// <summary>
		/// Lua (.lua) scripting language.
		/// The AsyncLua is used, which is an extended version of Lua with async/await support.
		/// </summary>
		Lua,

		// === EXTERNAL PROCESS SCRIPTS ===

		/// <summary>
		/// Python (.py) scripting language.
		/// External process is used to execute the script.
		/// Supports virtual environments.
		/// </summary>
		Python,

		/// <summary>
		/// JavaScript (.js) scripting language.
		/// NodeJS, Bun or Deno are used to execute the script.
		/// </summary>
		JavaScript
	}
}