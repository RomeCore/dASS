using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Localization;

namespace LLMDesktopAssistant.SlashCommands.Loading
{
	/// <summary>
	/// The addon type descriptor that registers the slash-command addon kind.
	/// </summary>
	[AddonTypeDescriptor]
	public class SlashCommandAddonTypeDescriptor : IAddonTypeDescriptor
	{
		public string Type => "commands";

		public AddonKind Kind => AddonKind.SlashCommand;

		public Type ClrType => typeof(SlashCommandInfo);

		public bool UseDefaultDiagnosticFactory => true;

		/// <summary>
		/// Commands are not BM25-searched in v1 — they are resolved by token, not searched.
		/// </summary>
		public bool UseDefaultSearchService => false;

		public LocaleKeyBase NameKey => Locale.GetKey("addon.type.commands.name");

		public LocaleKeyBase? DescriptionKey => Locale.GetKey("addon.type.commands.description");
	}
}
