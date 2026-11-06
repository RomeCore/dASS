using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.LLM.Services;

namespace LLMDesktopAssistant.Hooks
{
	[ChatService(typeof(IAddonSetCollector<HookInfo>))]
	public class HookSetCollector(
		IChatSettingsService chatSettings,
		IServiceProvider services
	) : AddonSetCollectorBase<HookInfo, HookChange>(services)
	{
		protected override AddonTarget ResolveChatTarget()
		{
			var settings = chatSettings.Settings.Hooks;
			return new(settings.GetEffectiveHooksSet(), settings);
		}
	}
}
