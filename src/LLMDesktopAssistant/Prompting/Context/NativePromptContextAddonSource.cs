using LLMDesktopAssistant.Addons.Sources;
using LLMDesktopAssistant.LLM.Services;

namespace LLMDesktopAssistant.Prompting.Context
{
	/// <summary>
	/// Contributes the prompt contexts of every native provider.
	/// </summary>
	[ChatService(typeof(IAdditionalAddonSource<PromptContextInfo>))]
	public class NativePromptContextAddonSource(IEnumerable<PromptContextNativeProvider> providers)
		: StaticAddonSource<PromptContextInfo>(providers.SelectMany(provider => provider.GetContexts()));
}
