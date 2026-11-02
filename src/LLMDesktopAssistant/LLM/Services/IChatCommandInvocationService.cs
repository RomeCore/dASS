using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.SlashCommands;
using LLMDesktopAssistant.SlashCommands.Arguments;
using LLMDesktopAssistant.SlashCommands.Execution;

namespace LLMDesktopAssistant.LLM.Services
{
	/// <summary>
	/// The outcome of resolving a command: the resolved command and the bound arguments when it succeeded, or the
	/// reason the send is blocked.
	/// </summary>
	public readonly record struct CommandResolution(
		SlashCommandInfo? Command,
		SlashCommandBoundArguments? Arguments,
		string RawArguments,
		LocaleKeyBase? Error,
		int ErrorPosition);

	/// <summary>
	/// The outcome of running a command: the final generation decision and what to record.
	/// </summary>
	public readonly record struct CommandExecution(
		bool Generate,
		SlashCommandExecutionStatus Status,
		LocaleKeyBase? Error,
		string? EffectSummary,
		ModelFacingMode? ModelFacingMode = null);

	/// <summary>
	/// The command machinery of the send pipeline: resolve a token, then execute it on an already-inserted message.
	/// It never appends a message and never touches the follow-up queue.
	/// </summary>
	public interface IChatCommandInvocationService
	{
		/// <summary>
		/// Resolves the token, parses and binds its arguments. The resolved command and the bound arguments are
		/// carried even when binding fails, so the fingerprint can describe the attempt.
		/// </summary>
		/// <param name="rawToken">The token without the leading slash.</param>
		/// <param name="rawArguments">The raw argument text.</param>
		CommandResolution Resolve(string rawToken, string rawArguments);

		/// <summary>
		/// Runs the resolved command on <paramref name="message"/> under the command execution level.
		/// A cancelled command is not a failure: it reports <see cref="SlashCommandExecutionStatus.Cancelled"/> and no
		/// error, and it never generates.
		/// </summary>
		/// <param name="resolution">The resolution to execute; its command must be present.</param>
		/// <param name="rawToken">The token exactly as the user typed it.</param>
		/// <param name="message">The already-inserted message the command belongs to.</param>
		/// <param name="generateIntent">The caller's generate intent; a command may only lower it.</param>
		/// <param name="ct">The caller's execution token.</param>
		Task<CommandExecution> ExecuteAsync(CommandResolution resolution, string rawToken, ChatMessage message,
			bool generateIntent, CancellationToken ct = default);
	}
}
