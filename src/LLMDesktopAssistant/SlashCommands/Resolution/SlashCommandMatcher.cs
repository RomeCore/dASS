using System.Collections.Immutable;

namespace LLMDesktopAssistant.SlashCommands.Resolution
{
	/// <summary>
	/// Turns the raw text of a message into a command token and matches tokens against commands. Pure and static, so
	/// the whole token/resolution contract is unit-testable without DI.
	/// </summary>
	/// <remarks>
	/// The token model is slash-free: the leading <c>/</c> is a marker of the message text, not part of the command's
	/// identity. This class is the one place that knows the marker, so the message-insertion service and the input
	/// autocomplete service both go through it. A message whose leading word starts with <c>//</c> is escaped — it is
	/// not a command, and <see cref="UnescapeLeadingSlash"/> turns it into the message the user meant.
	/// </remarks>
	public static class SlashCommandMatcher
	{
		/// <summary>
		/// The character that marks a message as a command.
		/// </summary>
		public const char Prefix = '/';

		/// <summary>
		/// Extracts the leading command token and the raw argument remainder from a message.
		/// </summary>
		/// <param name="rawText">The whole message text.</param>
		/// <param name="token">The slash-free token; empty when the call returns <see langword="false"/>.</param>
		/// <param name="rawArguments">
		/// The remainder after the token with the separating whitespace trimmed off the left, verbatim otherwise
		/// (newlines kept); empty when the call returns <see langword="false"/>.
		/// </param>
		/// <returns>
		/// <see langword="true"/> when the message is a command (its first non-whitespace character is <c>/</c> and it
		/// is not escaped); <see langword="false"/> otherwise.
		/// </returns>
		public static bool TryExtractToken(string rawText, out string token, out string rawArguments)
		{
			token = string.Empty;
			rawArguments = string.Empty;

			if (string.IsNullOrEmpty(rawText))
				return false;

			var start = SkipWhitespace(rawText, 0);
			if (start >= rawText.Length || rawText[start] != Prefix)
				return false;

			// "//…" is the escape: the message is not a command.
			if (start + 1 < rawText.Length && rawText[start + 1] == Prefix)
				return false;

			var end = start + 1;
			while (end < rawText.Length && !char.IsWhiteSpace(rawText[end]))
				end++;

			token = rawText[(start + 1)..end];
			rawArguments = rawText[SkipWhitespace(rawText, end)..];
			return true;
		}

		/// <summary>
		/// Undoes the <c>//</c> escape of a message: drops the first of the two leading slashes. Messages that are not
		/// escaped are returned unchanged. The whitespace before the marker is preserved.
		/// </summary>
		public static string UnescapeLeadingSlash(string rawText)
		{
			if (string.IsNullOrEmpty(rawText))
				return rawText;

			var start = SkipWhitespace(rawText, 0);
			if (start + 1 < rawText.Length && rawText[start] == Prefix && rawText[start + 1] == Prefix)
				return rawText.Remove(start, 1);

			return rawText;
		}

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
		/// Total order: <c>OverrideOrder</c> descending (the native &gt; scriptable &gt; derived tiers), then
		/// <c>Order</c> ascending, then <see cref="SlashCommandInfo.Key"/> ascending — so the outcome is deterministic
		/// and a true tie cannot occur. Namespace matching is set semantics and case-insensitive.
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

		private static int SkipWhitespace(string text, int index)
		{
			while (index < text.Length && char.IsWhiteSpace(text[index]))
				index++;
			return index;
		}

		private static bool MatchesName(SlashCommandInfo command, string name)
			=> string.Equals(command.Name, name, StringComparison.OrdinalIgnoreCase)
			|| command.Aliases.Any(alias => string.Equals(alias, name, StringComparison.OrdinalIgnoreCase));

		private static bool MatchesQualifiers(SlashCommandInfo command, ImmutableList<string> qualifiers)
			=> qualifiers.All(qualifier => command.Namespaces.Any(
				ns => string.Equals(ns, qualifier, StringComparison.OrdinalIgnoreCase)));
	}
}
