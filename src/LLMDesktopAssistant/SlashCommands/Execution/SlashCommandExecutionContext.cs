using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.SlashCommands.Arguments;

namespace LLMDesktopAssistant.SlashCommands.Execution
{
	/// <summary>
	/// Everything a slash command's executor needs: the target message (already inserted) and the command's bound
	/// arguments.
	/// </summary>
	public sealed class SlashCommandExecutionContext
	{
		/// <summary>
		/// The chat the command runs in.
		/// </summary>
		public required Chat Chat { get; init; }

		/// <summary>
		/// The target message, already inserted into the chat. The executor mutates it directly.
		/// </summary>
		public required ChatMessage Message { get; init; }

		/// <summary>
		/// The resolved command definition.
		/// </summary>
		public required SlashCommandInfo Command { get; init; }

		/// <summary>
		/// The canonical, slash-free form of the command, e.g. <c>skill:grilling</c>
		/// (<see cref="SlashCommandInfo.CanonicalToken"/>).
		/// </summary>
		public required string Token { get; init; }

		/// <summary>
		/// The command token exactly as the user typed it, slash-free (e.g. <c>grilling</c>).
		/// </summary>
		public required string RawToken { get; init; }

		/// <summary>
		/// The whole message text, the command token included, verbatim.
		/// </summary>
		public required string RawText { get; init; }

		/// <summary>
		/// The whole argument text after the token, verbatim — the pre-parse remainder
		/// (<see cref="SlashCommandParsedArguments.RawArguments"/>).
		/// </summary>
		public required string RawArguments { get; init; }

		/// <summary>
		/// The bound arguments: schema defaults applied, values validated and converted. Valid by the time the executor
		/// runs — an invalid argument set blocks the send before execution.
		/// </summary>
		public required SlashCommandBoundArguments Arguments { get; init; }

		/// <summary>
		/// The caller's generation intent — whether the user's send asked for generation. The final decision may lower
		/// it but never raise it.
		/// </summary>
		public required bool GenerateIntent { get; init; }

		/// <summary>
		/// The chat-scoped service provider.
		/// </summary>
		public required IServiceProvider Services { get; init; }
	}
}
