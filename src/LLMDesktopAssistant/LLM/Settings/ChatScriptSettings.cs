using LLMDesktopAssistant.SourceGenerators;

namespace LLMDesktopAssistant.LLM.Settings
{
	[SettingsRoute(nameof(ChatSettings.Scripts))]
	public partial class ChatScriptSettings : ChatSettingsCategoryBase
	{
		/// <summary>
		/// Gets or sets the script-set settings.
		/// </summary>
		[InheritedChatSetting]
		public LuaScriptsetConfiguration Scriptset
		{
			get => field ??= new();
			set => SetProperty(ref field, value);
		}
	}
}
