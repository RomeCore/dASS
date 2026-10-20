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
		/// Commands are agent-searchable as a read-only catalogue: an agent cannot run a command, but it can tell the
		/// user which one to type. See <see cref="SlashCommandAgenticSearchProvider"/>.
		/// </summary>
		public bool UseDefaultSearchService => true;

		public LocaleKeyBase NameKey => Locale.GetKey("addon.type.commands.name");

		public LocaleKeyBase? DescriptionKey => Locale.GetKey("addon.type.commands.description");
	}
}
