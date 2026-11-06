using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Hooks;
using LLMDesktopAssistant.SourceGenerators;

namespace LLMDesktopAssistant.LLM.Settings
{
	/// <summary>
	/// Chat-level hook settings: the master enable gate and the per-hook override bag.
	/// </summary>
	[SettingsRoute(nameof(ChatSettings.Hooks))]
	public partial class ChatHookSettings : ChatSettingsCategoryBase, IAddonCategorySettings
	{
		/// <summary>
		/// Whether hooks are enabled for this chat.
		/// </summary>
		public bool AddonsCategoryEnabled
		{
			get;
			set => SetProperty(ref field, value);
		} = true;

		/// <summary>
		/// The per-hook overrides (keyed by <see cref="HookSetConfiguration.Key"/>).
		/// </summary>
		[InheritedChatSetting]
		public HookSetConfiguration HooksSet
		{
			get;
			set => SetProperty(ref field, value);
		} = new();
	}
}
