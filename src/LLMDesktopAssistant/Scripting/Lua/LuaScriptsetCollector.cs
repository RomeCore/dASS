using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.LLM.Services;

namespace LLMDesktopAssistant.Scripting.Lua
{
	[ChatService(typeof(IAddonSetCollector<LuaScriptInfo>))]
	public class LuaScriptsetCollector(
		IChatSettingsService chatSettings,
		IServiceProvider services
	) : AddonSetCollectorBase<LuaScriptInfo, LuaScriptChange>(services)
	{
		protected override AddonTarget ResolveChatTarget()
			=> new(chatSettings.Settings.Scripts.GetEffectiveScriptset(), null);
	}
}
