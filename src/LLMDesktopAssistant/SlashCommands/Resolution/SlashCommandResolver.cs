using System.Collections.Immutable;
using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.Localization;

namespace LLMDesktopAssistant.SlashCommands.Resolution
{
	/// <summary>
	/// Resolves a slash-free command token against the chat's (enabled) command set.
	/// </summary>
	[ChatService(typeof(ISlashCommandResolver))]
	public class SlashCommandResolver(IAddonSetCollector<SlashCommandInfo> commands) : ISlashCommandResolver
	{
		/// <inheritdoc/>
		public SlashCommandResolution Resolve(string token)
		{
			var parsed = SlashCommandMatcher.ParseToken(token);
			var candidates = SlashCommandMatcher.Match(commands.GetAddonsForChat(), parsed);

			if (candidates.Count == 0)
			{
				return new SlashCommandResolution(
					null,
					Locale.GetFormattedKey("command.error.unknown", token),
					SlashCommandResolutionStatus.Unknown,
					[]);
			}

			var winner = candidates[0].Command;

			// The defeated set is the losing matches plus the winner's collapsed duplicates (the collector keeps them
			// in Overrides rather than as separate commands).
			var defeated = candidates
				.Skip(1)
				.Select(candidate => candidate.Command)
				.Concat(winner.Overrides)
				.ToList();

			return new SlashCommandResolution(
				winner,
				null,
				defeated.Count == 0 ? SlashCommandResolutionStatus.Exact : SlashCommandResolutionStatus.WonOthers,
				defeated);
		}
	}
}
