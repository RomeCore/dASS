using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Addons.MVVM;
using LLMDesktopAssistant.LLM.Settings;
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
	/// Gets the underlying chat prompt context settings.
	/// </summary>
	public ChatPromptContextSettings PromptContextSettings { get; }

	/// <summary>
	/// Gets the addon list that renders the available prompt contexts and searches over them.
	/// </summary>
	public AddonListViewModel List { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="ChatPromptContextsSettingsViewModel"/> class.
	/// </summary>
	/// <param name="services">The chat services used to build the addon list.</param>
	/// <param name="settings">The chat prompt context settings.</param>
	public ChatPromptContextsSettingsViewModel(IServiceProvider services, ChatPromptContextSettings settings)
	{
		PromptContextSettings = settings;

		List = new AddonListViewModel<PromptContextInfo, PromptContextChange>(services, null, AddonKind.PromptContext)
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
