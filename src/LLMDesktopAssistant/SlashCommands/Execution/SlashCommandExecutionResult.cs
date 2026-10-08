using LLMDesktopAssistant.Localization;

namespace LLMDesktopAssistant.SlashCommands.Execution
{
	/// <summary>
	/// The outcome of executing a slash command.
	/// </summary>
	/// <param name="Generate">
	/// Whether the command wants generation to happen after it. Combined with the caller's intent and the command's
	/// declarative ceiling, this is the final generation decision — it can only lower the intent.
	/// </param>
	/// <param name="Error">
	/// The user-facing error, as a locale key, or <see langword="null"/> on success. The host resolves it to text when
	/// writing the fingerprint and the message's <c>Error</c>.
	/// </param>
	public readonly record struct SlashCommandExecutionResult(bool Generate, LocaleKeyBase? Error)
	{
		/// <summary>
		/// A successful result, requesting generation by default.
		/// </summary>
		public static SlashCommandExecutionResult Ok(bool generate = true) => new(generate, null);

		/// <summary>
		/// Whether the command succeeded.
		/// </summary>
		public bool IsSuccess => Error is null;
	}
}
