using LLMDesktopAssistant.Hooks;
using LLMDesktopAssistant.Services;

namespace LLMDesktopAssistant.Scripting.Lua.Addons
{
	[Service(typeof(IScriptableHookEngine))]
	public class LuaHookEngine : IScriptableHookEngine
	{
		/// <inheritdoc/>
		public IReadOnlyList<IScriptEngineDescriptor> Descriptors { get; } = [LuaScriptEngineDescriptor.Instance];

		/// <inheritdoc/>
		public IHookExecutor CreateExecutor(HookInfo hook)
			=> new LuaScriptHookExecutor();
	}
}
