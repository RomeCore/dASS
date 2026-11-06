namespace LLMDesktopAssistant.Scripting.Lua.Addons
{
	public class LuaScriptEngineDescriptor : IScriptEngineDescriptor
	{
		private LuaScriptEngineDescriptor() { }

		public static LuaScriptEngineDescriptor Instance { get; } = new();

		public ScriptLanguageType Language => ScriptLanguageType.Lua;

		public string[] Extensions => [".lua", ".alua"];

		public string FrontmatterStart => "--[[";

		public string FrontmatterEnd => "]]";

		public string? FrontmatterLinePrefix => null;

		/// <remarks>
		/// Lua runs in-process, so it has no terminal and no output channel to take a structure from: a <c>json-output</c>
		/// or <c>run-terminal</c> promise cannot be honoured here and is refused at load. The structure a kind consumes
		/// is the script's return value, which is what <see cref="ScriptEngineCapabilities.StructuredOutput"/> declares and
		/// why a Lua addon never has to spell out how its result is taken out.
		/// </remarks>
		public ScriptEngineCapabilities Capabilities =>
			ScriptEngineCapabilities.StructuredInput | ScriptEngineCapabilities.StructuredOutput;

	}
}
