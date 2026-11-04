using LLMDesktopAssistant.Addons.Sources;
using LLMDesktopAssistant.LLM.Services;

namespace LLMDesktopAssistant.Tools
{
	/// <summary>
	/// Contributes the tools of every chat-scoped <see cref="ToolModule"/>.
	/// </summary>
	[ChatService(typeof(IAdditionalAddonSource<ToolInfo>))]
	public class ToolModuleAddonSource(IServiceProvider services)
		: StaticAddonSource<ToolInfo>(services.GetServices<ToolModule>().SelectMany(module => module.GetTools()));
}
