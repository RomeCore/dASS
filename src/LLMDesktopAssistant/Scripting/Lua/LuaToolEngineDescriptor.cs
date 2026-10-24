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
		/// Lua runs in-process, so it has no terminal and no output channel to take a structure from: a <c>json-output</c>
		/// or <c>run-terminal</c> promise cannot be honoured here and is refused at load. It does pass a native structure.
		/// </remarks>
		public ScriptEngineCapabilities Capabilities => ScriptEngineCapabilities.StructuredInput;
	}
}
