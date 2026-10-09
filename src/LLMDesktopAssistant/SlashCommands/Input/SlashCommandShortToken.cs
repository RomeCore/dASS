using System;
using System.Collections.Generic;
using LLMDesktopAssistant.SlashCommands.Resolution;

namespace LLMDesktopAssistant.SlashCommands.Input
{
	/// <summary>
	/// The shortest form a command can be written in without becoming ambiguous — the fully-qualified token
	/// (<c>agent:%LOCALAPPDATA%:web-searcher</c>) is usually longer than it needs to be, and only a command that shares
	/// its bare name with another needs the qualifier.
	/// </summary>
	/// <remarks>
	/// The form is derived from the resolver itself rather than from a naming rule: a candidate is accepted only when
	/// <see cref="SlashCommandMatcher.Match"/> resolves it back to the very same command, so what the input offers is
	/// exactly what typing it by hand would pick. Aliases are deliberately not considered — they stay a typing
	/// convenience and are never inserted.
	/// </remarks>
	public static class SlashCommandShortToken
	{
		/// <summary>
		/// The shortest suffix of <paramref name="command"/>'s canonical token — leading namespace segments dropped one
		/// at a time — that still resolves to that command among <paramref name="commands"/>.
		/// </summary>
		public static string For(SlashCommandInfo command, IReadOnlyList<SlashCommandInfo> commands)
		{
			ArgumentNullException.ThrowIfNull(command);
			ArgumentNullException.ThrowIfNull(commands);

			var segments = command.CanonicalToken.Split(':');

			// Shortest first: the bare name, then the pack qualifier, and so on up to the full token.
			for (var start = segments.Length - 1; start >= 0; start--)
			{
				var candidate = string.Join(':', segments[start..]);
				if (ResolvesTo(candidate, command, commands))
					return candidate;
			}

			return command.CanonicalToken;
		}

		private static bool ResolvesTo(string token, SlashCommandInfo command, IReadOnlyList<SlashCommandInfo> commands)
		{
			var candidates = SlashCommandMatcher.Match(commands, SlashCommandMatcher.ParseToken(token));
			return candidates.Count > 0 && ReferenceEquals(candidates[0].Command, command);
		}
	}
}
