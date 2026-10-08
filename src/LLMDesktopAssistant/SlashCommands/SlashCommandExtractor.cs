namespace LLMDesktopAssistant.SlashCommands
{
	/// <summary>
	/// Owns the <c>/</c> marker of a command message: it extracts the leading command token and the raw argument
	/// remainder, and undoes the <c>//</c> escape. Pure and static.
	/// </summary>
	/// <remarks>
	/// The token model is slash-free — the marker belongs to the message text, not to a command's identity. This is the
	/// single place that knows about <c>/</c>; <see cref="Resolution.SlashCommandMatcher"/> and everything downstream
	/// work on the slash-free token it produces. The message-insertion service is its consumer.
	/// </remarks>
	public static class SlashCommandExtractor
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

		private static int SkipWhitespace(string text, int index)
		{
			while (index < text.Length && char.IsWhiteSpace(text[index]))
				index++;
			return index;
		}
	}
}
