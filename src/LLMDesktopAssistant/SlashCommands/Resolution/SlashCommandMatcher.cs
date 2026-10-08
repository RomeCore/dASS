using System.Collections.Immutable;

namespace LLMDesktopAssistant.SlashCommands.Resolution
{
	/// <summary>
	/// Matches a slash-free command token against commands. Pure and static, so the whole matching contract is
	/// unit-testable without DI.
	/// </summary>
	/// <remarks>
	/// The token model is slash-free: the leading <c>/</c> marker of a message belongs to
	/// <see cref="SlashCommandExtractor"/>, which produces the token this class matches. This class never sees the
	/// marker.
	/// </remarks>
	public static class SlashCommandMatcher
	{
		/// <summary>
		/// Splits a slash-free token into its namespace qualifiers and its name. The token is valid when it carries a
		/// non-empty name and no empty segments.
		/// </summary>
		public static SlashCommandToken ParseToken(string token)
		{
			ArgumentNullException.ThrowIfNull(token);

			var segments = token.Split(':');
			var name = segments[^1];
			string[] qualifiers = segments.Length > 1 ? segments[..^1] : [];

			return new SlashCommandToken
			{
				Raw = token,
				Qualifiers = [.. qualifiers],
				Name = name,
				IsValid = name.Length > 0 && qualifiers.All(q => q.Length > 0)
			};
		}

		/// <summary>
		/// Every command whose name (or alias) matches the token and whose namespaces satisfy every qualifier, ordered
		/// by the total order. The first entry is the winner, the rest are defeated.
		/// </summary>
		/// <remarks>
		/// Total order: an exact name match before an alias-only match, then <c>OverrideOrder</c> descending (the
		/// native &gt; scriptable &gt; derived tiers), then <c>Order</c> ascending, then <see cref="SlashCommandInfo.Key"/>
		/// ascending — so the outcome is deterministic and a true tie cannot occur. Namespace matching is set semantics
		/// and case-insensitive.
		/// </remarks>
		public static ImmutableList<SlashCommandCandidate> Match(
			IEnumerable<SlashCommandInfo> commands, SlashCommandToken token)
		{
			ArgumentNullException.ThrowIfNull(commands);

			if (!token.IsValid)
				return [];

			var ordered = commands
				.Where(command => MatchesName(command, token.Name) && MatchesQualifiers(command, token.Qualifiers))
				.OrderByDescending(command => string.Equals(command.Name, token.Name, StringComparison.OrdinalIgnoreCase) ? 1 : 0)
				.ThenByDescending(command => command.OverrideOrder)
				.ThenBy(command => command.Order)
				.ThenBy(command => command.Key, StringComparer.Ordinal)
				.ToList();

			var builder = ImmutableList.CreateBuilder<SlashCommandCandidate>();
			for (var i = 0; i < ordered.Count; i++)
				builder.Add(new SlashCommandCandidate(ordered[i], IsDefeated: i > 0));

			return builder.ToImmutable();
		}

		private static bool MatchesName(SlashCommandInfo command, string name)
			=> string.Equals(command.Name, name, StringComparison.OrdinalIgnoreCase)
			|| command.Aliases.Any(alias => string.Equals(alias, name, StringComparison.OrdinalIgnoreCase));

		private static bool MatchesQualifiers(SlashCommandInfo command, ImmutableList<string> qualifiers)
			=> qualifiers.All(qualifier => command.Namespaces.Any(
				ns => string.Equals(ns, qualifier, StringComparison.OrdinalIgnoreCase)));
	}
}
