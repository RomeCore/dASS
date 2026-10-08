using LLMDesktopAssistant.SlashCommands;
using LLMDesktopAssistant.SourceGenerators;

namespace LLMDesktopAssistant.LLM.Settings
{
	/// <summary>
	/// Chat-level slash-command settings: the master enable gate and the per-command override bag.
	/// </summary>
	[SettingsRoute(nameof(ChatSettings.Commands))]
	public partial class ChatCommandSettings : ChatSettingsCategoryBase
	{
		/// <summary>
		/// Whether slash commands are enabled for this chat. Deliberately chat-local — the master gate is never
		/// inherited from the application.
		/// </summary>
		public bool EnableCommands
		{
			get;
			set => SetProperty(ref field, value);
		} = true;

		/// <summary>
		/// The per-command overrides (keyed by <see cref="SlashCommands.SlashCommandInfo.Key"/>).
		/// Inherits the application bag by default.
		/// </summary>
		[InheritedChatSetting]
		public SlashCommandSet CommandsSet
		{
			get;
			set => SetProperty(ref field, value);
		} = new();
	}
}
