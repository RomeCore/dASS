using LLMDesktopAssistant.Scripting;

namespace LLMDesktopAssistant.Desktop.Scripting.Python
{
	public class PythonToolEngineDescriptor : IScriptEngineDescriptor
	{
		public ScriptLanguageType Language => ScriptLanguageType.Python;

		public string[] Extensions => [".py"];

		public string FrontmatterStart => @"""""""";

		public string FrontmatterEnd => @"""""""";

		public string? FrontmatterLinePrefix => null;

		/// <remarks>
		/// Python passes a native structure, can report a structured result and can run in a terminal.
		/// </remarks>
		public ScriptEngineCapabilities Capabilities =>
			ScriptEngineCapabilities.StructuredInput | ScriptEngineCapabilities.StructuredOutput |
			ScriptEngineCapabilities.Terminal;
	}
}
