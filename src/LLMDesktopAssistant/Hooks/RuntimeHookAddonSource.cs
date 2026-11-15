using LLMDesktopAssistant.Addons.Sources;
using LLMDesktopAssistant.LLM.Services;

namespace LLMDesktopAssistant.Hooks
{
	/// <summary>
	/// The hooks a chat registered from Lua with <c>dass.hook.register</c>.
	/// </summary>
	/// <remarks>
	/// Chat-scoped: a runtime hook belongs to the chat that registered it and disappears with it. It feeds the same
	/// collector the pack hooks feed, so ordering, the per-chat overrides and the settings list need no branch on
	/// where a hook came from.
	/// </remarks>
	[ChatService]
	[ChatService(typeof(IAdditionalAddonSource<HookInfo>))]
	public class RuntimeHookAddonSource : RuntimeAddonSource<HookInfo>
	{
	}
}
