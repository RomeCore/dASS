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
		/// Commands are not agent-searchable for now, so we don't use the default search service for them.
		/// </summary>
		public bool UseDefaultSearchService => false;

		public LocaleKeyBase NameKey => Locale.GetKey("addon.type.commands.name");

		public LocaleKeyBase? DescriptionKey => Locale.GetKey("addon.type.commands.description");
	}
}
