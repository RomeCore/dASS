using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Addons.MVVM;
using LLMDesktopAssistant.LLM.Settings;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Tools;

namespace LLMDesktopAssistant.LLM.MVVM.Settings;

/// <summary>
/// ViewModel for the chat-level tool settings: the available tools rendered by the reusable addon list
/// (with the search box). The list is read-only here, since there is no chat-level toolset: enabling
/// and configuring tools is done per agent in <see cref="Agents.AgentToolSettingsViewModel"/>.
/// </summary>
[ViewModelFor(typeof(ChatToolsSettingsView))]
public class ChatToolsSettingsViewModel : ViewModelBase
{
	/// <summary>
	/// Gets the underlying chat tool settings.
	/// </summary>
	public ChatToolSettings ToolSettings { get; }

	/// <summary>
	/// Gets the addon list that renders the available tools and searches over them.
	/// </summary>
	public AddonListViewModel List { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="ChatToolsSettingsViewModel"/> class.
	/// </summary>
	/// <param name="services">The chat services used to build the addon list.</param>
	/// <param name="settings">The chat tool settings.</param>
	public ChatToolsSettingsViewModel(IServiceProvider services, ChatToolSettings settings)
	{
		ToolSettings = settings;

		List = new AddonListViewModel<ToolInfo, ToolChange>(services, null, AddonKind.Tool)
		{
			SearchPlaceholderKey = Locale.GetKey("settings.tools.search.placeholder"),
			EmptyTextKey = Locale.GetKey("settings.tools.empty")
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
