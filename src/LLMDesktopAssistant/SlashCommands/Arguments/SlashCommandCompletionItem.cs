using LLMDesktopAssistant.Localization;

namespace LLMDesktopAssistant.SlashCommands.Arguments
{
	/// <summary>
	/// A single item offered by an <see cref="ISlashCommandArgumentFormatProvider"/> while completing an argument.
	/// </summary>
	public sealed class SlashCommandCompletionItem
	{
		/// <summary>
		/// The value inserted into the input when the item is accepted.
		/// </summary>
		public required string Value { get; init; }

		/// <summary>
		/// The text shown in the completion list. Falls back to <see cref="Value"/> when not set.
		/// </summary>
		public string? Display { get; init; }

		/// <summary>
		/// The locale key of the item's description, if any.
		/// </summary>
		public LocaleKeyBase? Description { get; init; }
	}
}
