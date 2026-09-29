using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Addons.Management;
using LLMDesktopAssistant.Addons.MVVM;
using LLMDesktopAssistant.Addons.Search;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Prompting.Context;

namespace LLMDesktopAssistant.LLM.MVVM.Settings;

/// <summary>
/// ViewModel for the chat-level prompt context settings: the available prompt contexts rendered by the
/// reusable addon list (with the search box). The list is read-only here, since enabling and disabling
/// prompt contexts is done per agent in <see cref="Agents.AgentContextSettingsViewModel"/>.
/// </summary>
[ViewModelFor(typeof(ChatPromptContextsView))]
public class ChatPromptContextsSettingsViewModel : ViewModelBase
{
	/// <summary>
	/// Gets the addon list that renders the available prompt contexts and searches over them.
	/// </summary>
	public AddonListViewModel List { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="ChatPromptContextsSettingsViewModel"/> class.
	/// </summary>
	/// <param name="promptContextCollector">The collector that provides the available prompt contexts.</param>
	/// <param name="cardFactory">The factory that builds the prompt context cards.</param>
	/// <param name="addonInvalidator">The invalidator used to reload the addons before building the list.</param>
	/// <param name="searchService">The search service used to filter the list by the search query.</param>
	public ChatPromptContextsSettingsViewModel(IAddonSetCollector<PromptContextInfo> promptContextCollector,
		IAddonCardFactory<PromptContextInfo, PromptContextChange> cardFactory,
		IAddonManagerInvalidator addonInvalidator,
		IAddonSearchService<PromptContextInfo> searchService)
	{
		List = new AddonListViewModel<PromptContextInfo, PromptContextChange>(promptContextCollector, cardFactory,
			addonInvalidator, AddonKind.PromptContext, searchService)
		{
			SearchPlaceholderKey = Locale.GetKey("settings.prompt_contexts.search.placeholder"),
			EmptyTextKey = Locale.GetKey("settings.prompt_contexts.empty")
		};
		List.Update();
	}

	/// <inheritdoc/>
	protected override void Dispose(bool disposing)
	{
		base.Dispose(disposing);

		if (disposing)
			List.Dispose();
	}
}
