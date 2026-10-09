using LLMDesktopAssistant.InputCompletion;

namespace LLMDesktopAssistant.SlashCommands.Input
{
	/// <summary>
	/// What a piece of input text looks like to the slash-command layer: whether it is a command, the region of its
	/// token and arguments, how the token resolves, and where the caret sits.
	/// </summary>
	/// <remarks>
	/// <see cref="ResolutionState"/> is computed from the text alone (caret-independent), so the highlight transform —
	/// which receives the text but not the caret — can colour the token. The caret flags are for the popup, which
	/// decides when to open and when it has switched to argument mode. Offsets are in raw-text coordinates.
	/// </remarks>
	public sealed class SlashCommandInputAnalysis
	{
		/// <summary>The analysis of text that is not a command.</summary>
		public static readonly SlashCommandInputAnalysis NotACommand = new();

		/// <summary>Whether the text is a command message (a leading <c>/</c> that is not the <c>//</c> escape).</summary>
		public bool IsCommand { get; init; }

		/// <summary>The command token without the leading <c>/</c>; empty when <see cref="IsCommand"/> is false.</summary>
		public string Token { get; init; } = string.Empty;

		/// <summary>The span of the token, the leading <c>/</c> included.</summary>
		public InputCompletionSpan TokenSpan { get; init; }

		/// <summary>The span of the argument text after the token (the separating whitespace excluded); may be empty.</summary>
		public InputCompletionSpan ArgumentSpan { get; init; }

		/// <summary>How the token resolves; <see cref="SlashCommandInputResolutionState.None"/> when not a command.</summary>
		public SlashCommandInputResolutionState ResolutionState { get; init; }

		/// <summary>
		/// The command the token resolves to (<see cref="SlashCommandInputResolutionState.Known"/> or
		/// <see cref="SlashCommandInputResolutionState.WonOthers"/>); <see langword="null"/> otherwise.
		/// </summary>
		public SlashCommandInfo? Command { get; init; }

		/// <summary>Whether the caret sits inside the token (its end included).</summary>
		public bool IsCaretInToken { get; init; }

		/// <summary>Whether the caret sits after the token, in the argument region.</summary>
		public bool IsCaretInArguments { get; init; }
	}
}
