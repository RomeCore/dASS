using LLMDesktopAssistant.Scripting;

namespace LLMDesktopAssistant.Desktop.Scripting.ExternalProcess
{
	/// <summary>
	/// What a language engine needs to run a script as a managed process: the terminal parameters plus the output
	/// contract.
	/// </summary>
	public class ExternalProcessScriptParameters : ExternalProcessScriptTerminalParameters
	{
		/// <summary>
		/// Where the structured result is taken from.
		/// </summary>
		public ScriptOutputMode OutputMode { get; init; } = ScriptOutputMode.None;

		/// <summary>
		/// The extraction pattern, only with <see cref="ScriptOutputMode.Pattern"/>: <c>"&lt;BEGIN&gt;*&lt;END&gt;"</c>.
		/// <c>\*</c> is a literal asterisk; the first unescaped <c>*</c> splits the pattern.
		/// </summary>
		public string? OutputPattern { get; init; }
	}
}
