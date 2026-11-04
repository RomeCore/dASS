using LLMDesktopAssistant.Agents;

namespace LLMDesktopAssistant.Addons
{
	public interface IAddonSetCollector<T>
		where T : AddonBase<T>
	{
		AddonSet<T> GetAvailableAddons();

		AddonSet<T> GetAddonsForChat();

		AddonSet<T> GetAddonsForAgent(ChatAgentDescriptor agent);
	}
}
