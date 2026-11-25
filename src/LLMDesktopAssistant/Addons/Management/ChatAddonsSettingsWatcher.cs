using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.Settings.Application;

namespace LLMDesktopAssistant.Addons.Management
{
	/// <summary>
	/// Watches the addon settings of the application and of the current chat and refreshes the addon managers when
	/// they change or when the chat settings instance is replaced by a settings profile switch.
	/// </summary>
	/// <remarks>
	/// A profile switch replaces the chat settings instance instead of mutating it, so the watcher re-hooks the new
	/// instance on <see cref="IChatSettingsService.SettingsChanged"/> and detaches the old one - otherwise it keeps
	/// observing a profile that is no longer loaded.
	/// </remarks>
	[ChatService]
	public class ChatAddonsSettingsWatcher : Disposable
	{
		public ChatAddonsSettingsWatcher(IChatSettingsService chatSettings, IAddonManagerInvalidator invalidator)
		{
			var appAddonsSettings = ApplicationSettingsAccessor.ApplicationSettings.InheritedChatSettings.Addons;

			Action? unsubscribeChatAddons = null;

			void InvalidateAll()
			{
				// TODO: Invalidate only kinds that changed, but it's too complex for now
				invalidator.Invalidate(AddonKind.All);
			}

			void OnAppAddonsDeepChanged(object? sender, EventArgs e) => InvalidateAll();

			void OnChatAddonsDeepChanged(object? sender, EventArgs e) => InvalidateAll();

			void AttachChatAddons()
			{
				unsubscribeChatAddons?.Invoke();

				var addonsSettings = chatSettings.Settings.Addons;
				addonsSettings.DeepChanged += OnChatAddonsDeepChanged;
				unsubscribeChatAddons = () => addonsSettings.DeepChanged -= OnChatAddonsDeepChanged;
			}

			void SettingsChanged(object? sender, EventArgs e)
			{
				// Reload, not Invalidate: the replaced instance can drop the packs the loaded addons came from, and a
				// lazy reload would wait for the next fetch of an affected kind.
				invalidator.Reload(AddonKind.All);
				AttachChatAddons();
			}

			appAddonsSettings.DeepChanged += OnAppAddonsDeepChanged;
			AttachChatAddons();
			chatSettings.SettingsChanged += SettingsChanged;

			OnDispose += (s, e) =>
			{
				chatSettings.SettingsChanged -= SettingsChanged;
				appAddonsSettings.DeepChanged -= OnAppAddonsDeepChanged;
				unsubscribeChatAddons?.Invoke();
				unsubscribeChatAddons = null;
			};
		}
	}
}
