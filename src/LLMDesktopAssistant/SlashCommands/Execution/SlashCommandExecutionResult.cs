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
	/// <param name="EffectSummary">
	/// An optional short human-readable summary of what the command did, recorded in the fingerprint (for example
	/// "sub-agent task launched"). <see langword="null"/> when the executor has nothing to add.
	/// </param>
	/// <param name="ModelFacingMode">
	/// An optional override of how the command's message is presented to the model. <see langword="null"/> keeps the
	/// command's declarative <see cref="SlashCommandInfo.ModelFacingMode"/>; an executor picks a mode here (for example
	/// <see cref="ModelFacingMode.Neutral"/> to hide the raw text while keeping the injected content).
	/// </param>
	public readonly record struct SlashCommandExecutionResult(bool Generate, LocaleKeyBase? Error,
		string? EffectSummary = null, ModelFacingMode? ModelFacingMode = null)
	{
		/// <summary>
		/// A successful result, requesting generation by default.
		/// </summary>
		public static SlashCommandExecutionResult Ok(bool generate = true, string? effectSummary = null, ModelFacingMode? modelFacingMode = null)
			=> new(generate, null, effectSummary, modelFacingMode);

		/// <summary>
		/// Whether the command succeeded.
		/// </summary>
		public bool IsSuccess => Error is null;
	}
}
