using LLMDesktopAssistant.Tools.Scripting;

namespace LLMDesktopAssistant.Scripting.Lua
{
	public class LuaToolEngineDescriptor : IScriptableToolEngineDescriptor
	{
		public ScriptLanguageType Language => ScriptLanguageType.Lua;

		public string[] Extensions => [ ".lua", ".alua" ];

		public string FrontmatterStart => "--[[";

		public string FrontmatterEnd => "]]";
	}
}
