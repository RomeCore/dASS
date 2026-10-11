using LLMDesktopAssistant.SourceGenerators;

namespace LLMDesktopAssistant.LLM.Settings
{
	/// <summary>
	/// The settings that related to prompt contexts in the chat application.
	/// </summary>
	[SettingsRoute(nameof(ChatSettings.PromptContexts))]
	public partial class ChatPromptContextSettings : ChatSettingsCategoryBase
	{
		// Пока пусто...
	}
}
