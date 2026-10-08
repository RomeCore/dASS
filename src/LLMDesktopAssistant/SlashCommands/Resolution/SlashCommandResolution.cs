using LLMDesktopAssistant.Localization;

namespace LLMDesktopAssistant.SlashCommands.Resolution
{
	/// <summary>
	/// The result of resolving a command token.
	/// </summary>
	/// <param name="Command">The winning command, or <see langword="null"/> when nothing matched.</param>
	/// <param name="Error">The error, only for <see cref="SlashCommandResolutionStatus.Unknown"/>.</param>
	/// <param name="Status">The resolution status.</param>
	/// <param name="Defeated">
	/// The commands the winner beat: other matches plus the winner's collapsed duplicates. Empty for
	/// <see cref="SlashCommandResolutionStatus.Exact"/>.
	/// </param>
	public sealed record SlashCommandResolution(
		SlashCommandInfo? Command,
		LocaleKeyBase? Error,
		SlashCommandResolutionStatus Status,
		IReadOnlyList<SlashCommandInfo> Defeated);
}
