using LLMDesktopAssistant.SlashCommands.Arguments;

namespace LLMDesktopAssistant.SlashCommands.Execution
{
	/// <summary>
	/// Executes a single slash command's action against the message it was triggered from.
	/// </summary>
	/// <remarks>
	/// The executor <em>mutates the target message directly</em> through the context (appends content, attaches
	/// sub-agent tasks, ...). The host owns insertion, the execution token and the hand-off to generation; the
	/// executor only reports whether generation should follow.
	/// </remarks>
	public interface ISlashCommandExecutor
	{
		/// <summary>
		/// The argument schema of the command, or <see langword="null"/> when it takes no arguments.
		/// </summary>
		public SlashCommandArgumentSchema? ArgumentSchema { get; }

		/// <summary>
		/// Runs the command.
		/// </summary>
		/// <param name="ctx">The execution context — the target message and its bound arguments.</param>
		/// <param name="ct">The command-level execution token.</param>
		Task<SlashCommandExecutionResult> ExecuteAsync(SlashCommandExecutionContext ctx, CancellationToken ct);
	}
}
