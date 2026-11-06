using LLMDesktopAssistant.Scripting;

namespace LLMDesktopAssistant.Hooks
{
	public interface IScriptableHookEngine : IScriptableAddonEngine
	{
		IHookExecutor CreateExecutor(HookInfo context);
	}
}
