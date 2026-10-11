using LLMDesktopAssistant.Scripting;

namespace LLMDesktopAssistant.Tools.Scripting
{
	public interface IScriptableToolEngineDescriptor
	{
		ScriptLanguageType Language { get; }

		string[] Extensions { get; }

		string FrontmatterStart { get; }

		string FrontmatterEnd { get; }
	}
}
