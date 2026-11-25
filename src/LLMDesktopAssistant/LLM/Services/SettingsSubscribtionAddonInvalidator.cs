using LLMDesktopAssistant.Addons.Management;

namespace LLMDesktopAssistant.LLM.Services
{
	[ChatService]
	public class SettingsSubscribtionAddonInvalidator : Disposable
	{
		public SettingsSubscribtionAddonInvalidator(
			IAddonManagerInvalidator invalidator,
			IChatSettingsService settings
		)
		{
			void SettingsChanged(object? sender, EventArgs e)
			{
				invalidator.Invalidate(Addons.AddonKind.All);
			}

			settings.SettingsChanged += SettingsChanged;

			OnDispose += (s, e) =>
			{
				settings.SettingsChanged -= SettingsChanged;
			};
		}
	}
}
