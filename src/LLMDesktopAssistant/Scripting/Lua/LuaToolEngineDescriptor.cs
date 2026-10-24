namespace LLMDesktopAssistant.Scripting.Lua
{
	public class LuaToolEngineDescriptor : IScriptEngineDescriptor
	{
		public ScriptLanguageType Language => ScriptLanguageType.Lua;

		public string[] Extensions => [".lua", ".alua"];

		public string FrontmatterStart => "--[[";

		public string FrontmatterEnd => "]]";

		public string? FrontmatterLinePrefix => null;

		/// <remarks>
		/// Lua runs in-process, so there is no terminal to hand it to; it passes a native structure and can report a
		/// structured result.
		/// </remarks>
		public ScriptEngineCapabilities Capabilities =>
			ScriptEngineCapabilities.StructuredInput | ScriptEngineCapabilities.StructuredOutput;
	}
}
