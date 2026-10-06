using LLMDesktopAssistant.Localization;

namespace LLMDesktopAssistant.SlashCommands.Arguments
{
	/// <summary>
	/// Describes a single command argument: its display name, whether it is required, its default value and the
	/// optional format provider that validates, converts and completes it.
	/// </summary>
	/// <remarks>
	/// Immutable on purpose. A command's argument schema is atomic and travels with the command definition: a command is
	/// (re)defined by re-parsing its file or re-synthesising it in a provider, never by mutating an existing schema.
	/// </remarks>
	public class SlashCommandArgument
	{
		/// <summary>
		/// The locale key of the argument's display name.
		/// </summary>
		public required LocaleKeyBase Name { get; init; }

		/// <summary>
		/// The locale key of the argument's description, if any.
		/// </summary>
		public LocaleKeyBase? Description { get; init; }

		/// <summary>
		/// Whether the argument must be present for the command to run.
		/// </summary>
		public bool Required { get; init; }

		/// <summary>
		/// The default raw value used when the argument is absent. Authored defaults are trusted: they are converted
		/// but never validated.
		/// </summary>
		public string? Default { get; init; }

		/// <summary>
		/// The optional format provider that validates, converts and completes the argument's value.
		/// </summary>
		public ISlashCommandArgumentFormatProvider? Format { get; init; }
	}
}
