using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.SlashCommands;
using LLMDesktopAssistant.SourceGenerators;

namespace LLMDesktopAssistant.LLM.Settings
{
	/// <summary>
	/// Chat-level slash-command settings: the master enable gate and the per-command override bag.
	/// </summary>
	[SettingsRoute(nameof(ChatSettings.Commands))]
	public partial class ChatCommandSettings : ChatSettingsCategoryBase, IAddonCategorySettings
	{
		/// <summary>
		/// Whether slash commands are enabled for this chat.
		/// </summary>
		public bool AddonsCategoryEnabled
		{
			get;
			set => SetProperty(ref field, value);
		} = true;

		/// <summary>
		/// The per-command overrides (keyed by <see cref="SlashCommandInfo.Key"/>).
		/// </summary>
		[InheritedChatSetting]
		public SlashCommandSetConfiguration CommandsSet
		{
			get;
			set => SetProperty(ref field, value);
		} = new();
	}
}
