using System.Collections.Generic;
using System.Collections.Immutable;

namespace LLMDesktopAssistant.SlashCommands.Input
{
	/// <summary>
	/// Matches a partially typed, slash-free command token against commands — the "could this still become one?"
	/// question, as opposed to <see cref="Resolution.SlashCommandMatcher"/>'s exact matching. Pure and static.
	/// </summary>
	/// <remarks>
	/// Token grammar recap: the last segment is the name, every preceding segment is a namespace qualifier and must
	/// match one of the command's namespaces (order-independent). While typing, a segment may be an incomplete
	/// namespace. So a command matches a prefix when every segment but the last is a case-insensitive prefix of a
	/// distinct namespace, and the last segment is a prefix of a distinct namespace or of the name/an alias.
	/// </remarks>
	public static class SlashCommandPrefixMatcher
	{
		/// <summary>
		/// Every command the typed token could still be completed to, in the input order. An empty token matches
		/// everything (nothing has been typed yet).
		/// </summary>
		public static ImmutableList<SlashCommandInfo> Match(IEnumerable<SlashCommandInfo> commands, string token)
		{
			ArgumentNullException.ThrowIfNull(commands);

			if (string.IsNullOrEmpty(token))
				return [.. commands];

			var segments = token.Split(':');
			var builder = ImmutableList.CreateBuilder<SlashCommandInfo>();
			foreach (var command in commands)
			{
				if (MatchesPrefix(command, segments))
					builder.Add(command);
			}

			return builder.ToImmutable();
		}

		/// <summary>Whether a single command could still be completed from the typed token.</summary>
		public static bool MatchesPrefix(SlashCommandInfo command, string token)
		{
			ArgumentNullException.ThrowIfNull(command);
			return string.IsNullOrEmpty(token) || MatchesPrefix(command, token.Split(':'));
		}

		private static bool MatchesPrefix(SlashCommandInfo command, string[] segments)
			=> TryMatch(command, segments, 0, new HashSet<string>(StringComparer.OrdinalIgnoreCase));

		private static bool TryMatch(SlashCommandInfo command, string[] segments, int index, HashSet<string> usedNamespaces)
		{
			if (index == segments.Length)
				return true;

			var segment = segments[index];
			var isLast = index == segments.Length - 1;

			// The segment is (a prefix of) a not-yet-used namespace.
			foreach (var ns in command.Namespaces)
			{
				if (usedNamespaces.Contains(ns) || !ns.StartsWith(segment, StringComparison.OrdinalIgnoreCase))
					continue;

				usedNamespaces.Add(ns);
				if (TryMatch(command, segments, index + 1, usedNamespaces))
					return true;
				usedNamespaces.Remove(ns);
			}

			// Only the last segment may be (a prefix of) the name / an alias.
			if (isLast && MatchesNamePrefix(command, segment))
				return true;

			return false;
		}

		private static bool MatchesNamePrefix(SlashCommandInfo command, string segment)
			=> command.Name.StartsWith(segment, StringComparison.OrdinalIgnoreCase)
				|| command.Aliases.Any(alias => alias.StartsWith(segment, StringComparison.OrdinalIgnoreCase));
	}
}
