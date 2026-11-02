using LLMDesktopAssistant.LLM.Domain;

namespace LLMDesktopAssistant.LLM.Services
{
	/// <summary>
	/// Puts a message into the history and, as a separate step, runs the command the message carries.
	/// <para/>
	/// The two steps are separate because they are different things: appending is a synchronous write that never
	/// blocks, a command can run for minutes. The follow-up queue depends on the split - it drops an entry between
	/// its message reaching the history and its command running, so the queue never shows an already-inserted message.
	/// </para>
	/// <remarks>
	/// <see cref="CanInsertUserInput"/> is a pure pre-flight the view model calls <em>before</em> it commits the
	/// draft or enqueues it, so a refused command never loses the user's text. The append re-resolves, so a caller
	/// that skips the pre-flight (or a race with addon reloading) still gets a defined result: the message is
	/// inserted with the error attached and the command is not run.
	/// </remarks>
	public interface IChatMessageInsertionService
	{
		/// <summary>
		/// Checks whether <paramref name="input"/> can be inserted. Pure and side-effect free.
		/// </summary>
		/// <param name="input">The user input to check.</param>
		UserInputInsertionCheckResult CanInsertUserInput(UserInput input);

		/// <summary>
		/// Appends <paramref name="input"/> (or replaces the message at <paramref name="editIndex"/>) and resolves its
		/// command, without running it. Runs no model call and no tool.
		/// </summary>
		/// <param name="input">The user input to insert.</param>
		/// <param name="editIndex">The edited message index, or <see langword="null"/> for a new message.</param>
		/// <param name="ct">The cancellation token (the caller's execution token).</param>
		Task<UserInputInsertion> AppendUserInputAsync(UserInput input, int? editIndex = null,
			CancellationToken ct = default);

		/// <summary>
		/// Runs the command of an already-appended message and reports the generation intent it left behind. Never
		/// generates. A message whose <see cref="UserInput.CanExecuteCommand"/> was <see langword="false"/>, or that
		/// carries no command token, is a no-op that returns the incoming intent unchanged.
		/// </summary>
		/// <param name="insertion">The handle returned by <see cref="AppendUserInputAsync"/>.</param>
		/// <param name="generateIntent">The caller's generate intent; a command may only lower it.</param>
		/// <param name="ct">The cancellation token (the caller's execution token).</param>
		Task<UserInputInsertionResult> RunInsertedCommandAsync(UserInputInsertion insertion, bool generateIntent,
			CancellationToken ct = default);
	}
}
