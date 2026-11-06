using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Localization;

namespace LLMDesktopAssistant.Hooks
{
	/// <summary>
	/// The addon type descriptor that registers the hook addon type ('hooks') in the addon system.
	/// </summary>
	[AddonTypeDescriptor]
	public class HookAddonTypeDescriptor : IAddonTypeDescriptor
	{
		public string Type => "hooks";

		public AddonKind Kind => AddonKind.Hook;

		public Type ClrType => typeof(HookInfo);

		public bool UseDefaultDiagnosticFactory => true;

		public bool UseDefaultSearchService => true;

		public LocaleKeyBase NameKey => Locale.GetKey("addon.type.hooks.name");

		public LocaleKeyBase? DescriptionKey => Locale.GetKey("addon.type.hooks.description");
	}
}
