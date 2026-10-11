using LLMDesktopAssistant.Scripting;
using LLMDesktopAssistant.Tools.Scripting;

namespace LLMDesktopAssistant.Desktop.Scripting.Python
{
	public class PythonToolEngineDescriptor : IScriptableToolEngineDescriptor
	{
		public ScriptLanguageType Language => ScriptLanguageType.Python;

		public string[] Extensions => [".py"];

		public string FrontmatterStart => @"""""""";

		public string FrontmatterEnd => @"""""""";
	}
}
