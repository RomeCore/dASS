using System.Collections.Generic;
using LLMDesktopAssistant.InputCompletion;
using LLMDesktopAssistant.SlashCommands.Resolution;

namespace LLMDesktopAssistant.SlashCommands.Input
{
	/// <summary>
	/// Pure analysis of the raw input text from the slash-command point of view: is it a command, where are its token
	/// and arguments, how does the token resolve, and where is the caret. Consumed by the highlight transform (token
	/// and argument spans, resolution state) and by the completion source (caret flags, token/argument regions).
	/// </summary>
	public static class SlashCommandInputAnalyzer
	{
		/// <summary>
		/// Analyses <paramref name="text"/> at <paramref name="caretIndex"/> against the chat's commands.
		/// </summary>
		public static SlashCommandInputAnalysis Analyze(string? text, int caretIndex,
			IReadOnlyList<SlashCommandInfo> commands)
		{
			ArgumentNullException.ThrowIfNull(commands);

			text ??= string.Empty;
			caretIndex = Math.Clamp(caretIndex, 0, text.Length);

			if (!SlashCommandExtractor.TryExtractToken(text, out var token, out _))
				return SlashCommandInputAnalysis.NotACommand;

			var slashStart = SkipWhitespace(text, 0);
			var tokenEnd = slashStart + 1 + token.Length;
			var argumentStart = SkipWhitespace(text, tokenEnd);

			return new SlashCommandInputAnalysis
			{
				IsCommand = true,
				Token = token,
				TokenSpan = new InputCompletionSpan(slashStart, tokenEnd - slashStart),
				ArgumentSpan = new InputCompletionSpan(argumentStart, text.Length - argumentStart),
				ResolutionState = Resolve(token, commands),
				IsCaretInToken = caretIndex >= slashStart && caretIndex <= tokenEnd,
				IsCaretInArguments = caretIndex > tokenEnd
			};
		}

		private static SlashCommandInputResolutionState Resolve(string token, IReadOnlyList<SlashCommandInfo> commands)
		{
			if (token.Length == 0)
				return SlashCommandInputResolutionState.Partial;

			var candidates = SlashCommandMatcher.Match(commands, SlashCommandMatcher.ParseToken(token));
			if (candidates.Count > 0)
			{
				var winner = candidates[0].Command;
				var wonOthers = candidates.Count > 1 || winner.Overrides.Count > 0;
				return wonOthers
					? SlashCommandInputResolutionState.WonOthers
					: SlashCommandInputResolutionState.Known;
			}

			return SlashCommandPrefixMatcher.Match(commands, token).Count > 0
				? SlashCommandInputResolutionState.Partial
				: SlashCommandInputResolutionState.Unknown;
		}

		private static int SkipWhitespace(string text, int index)
		{
			while (index < text.Length && char.IsWhiteSpace(text[index]))
				index++;
			return index;
		}
	}
}
