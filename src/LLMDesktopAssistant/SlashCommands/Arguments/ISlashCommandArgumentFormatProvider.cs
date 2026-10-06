using LLMDesktopAssistant.Localization;

namespace LLMDesktopAssistant.SlashCommands.Arguments
{
	/// <summary>
	/// Per-argument logic of a command argument: validation, conversion and (optionally) completion.
	/// </summary>
	/// <remarks>
	/// A format provider handles one argument kind (a boolean flag, a file path, "one of many", ...). Adding a new kind
	/// of argument means adding a provider — the command engine itself is never touched.
	/// </remarks>
	public interface ISlashCommandArgumentFormatProvider
	{
		/// <summary>
		/// Validates a raw, user-supplied argument value.
		/// </summary>
		/// <param name="raw">The raw value, as written by the user (grouping quotes already stripped).</param>
		/// <param name="error">When the method returns <see langword="false"/>, the locale key of the reason.</param>
		/// <returns><see langword="true"/> when the value is acceptable.</returns>
		bool TryValidate(string raw, out LocaleKeyBase? error);

		/// <summary>
		/// Converts the raw value into the argument's value. Called only after <see cref="TryValidate"/> succeeded.
		/// </summary>
		/// <param name="raw">The raw value, as written by the user.</param>
		/// <returns>The converted value, stored in <see cref="ParsedSlashCommandArgument.Value"/>.</returns>
		object? Convert(string raw);

		/// <summary>
		/// Whether this provider can produce completions for its argument.
		/// </summary>
		bool CanComplete { get; }

		/// <summary>
		/// Produces completions for the given prefix. Completion is UX-only and never blocks anything.
		/// </summary>
		/// <param name="prefix">The part of the argument value typed so far.</param>
		/// <param name="ctx">The context of the completion request.</param>
		IEnumerable<SlashCommandCompletionItem> Complete(string prefix, SlashCommandCompletionContext ctx);
	}
}
