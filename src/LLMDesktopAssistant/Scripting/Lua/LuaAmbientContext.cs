namespace LLMDesktopAssistant.Scripting.Lua
{
	public class LuaAmbientContext
	{
		public required LuaService Lua { get; init; }

		public required IServiceProvider Services { get; init; }
	}
}
