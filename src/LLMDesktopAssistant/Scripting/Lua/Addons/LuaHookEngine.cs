using LLMDesktopAssistant.Hooks;
using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.SlashCommands.Scripting;

namespace LLMDesktopAssistant.Scripting.Lua.Addons
{
	[Service(typeof(IScriptableHookEngine))]
	public class LuaHookEngine : IScriptableHookEngine
	{
		/// <inheritdoc/>
		public IReadOnlyList<IScriptEngineDescriptor> Descriptors { get; } = [LuaScriptEngineDescriptor.Instance];

		/// <inheritdoc/>
		public IHookExecutor CreateExecutor(HookInfo hook)
			=> new LuaScriptHookExecutor(hook);
	}
}
