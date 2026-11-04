using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Addons.Management;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.Settings.Application;

namespace LLMDesktopAssistant.Scripting.Lua
{
	/// <summary>
	/// The watcher for Lua script-related configuration that automatically triggers updates on
	/// <see cref="IAddonSetCollector{T}"/> for Lua scripts.
	/// </summary>
	[ChatService]
	public class LuaScriptsetStateWatcher : Disposable
	{
		private readonly IAddonSetCollector<LuaScriptInfo> _scriptSetCollector;
		private readonly IChatSettingsService _chatSettings;
		private readonly AddonManagerInvalidationHook _addonHook;

		public event Action? OnStateChanged;

		public LuaScriptsetStateWatcher(IAddonSetCollector<LuaScriptInfo> scriptSetCollector,
			IChatSettingsService chatSettings, AddonManagerInvalidationHook addonHook)
		{
			_scriptSetCollector = scriptSetCollector;
			_chatSettings = chatSettings;
			_addonHook = addonHook;

			_chatSettings.Settings.Scripts.DeepChanged += ScriptsSettings_DeepChanged;
			ApplicationSettingsAccessor.ApplicationSettings.InheritedChatSettings.Scripts.DeepChanged += ScriptsSettings_DeepChanged;
			_addonHook.OnReloaded += AddonHook_OnReloaded;
		}

		private void ScriptsSettings_DeepChanged(object? sender, EventArgs e)
		{
			// Trigger reloading on settings change (settings object compare is lazy)
			_scriptSetCollector.GetAddonsForChat();
		}

		private void AddonHook_OnReloaded(AddonKind kinds, bool obj)
		{
			_scriptSetCollector.GetAddonsForChat();
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (disposing)
			{
				_chatSettings.Settings.Scripts.DeepChanged -= ScriptsSettings_DeepChanged;
				ApplicationSettingsAccessor.ApplicationSettings.InheritedChatSettings.Scripts.DeepChanged -= ScriptsSettings_DeepChanged;
				_addonHook.OnReloaded -= AddonHook_OnReloaded;
			}
		}
	}
}