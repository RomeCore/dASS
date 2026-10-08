using LLMDesktopAssistant.LLM.Domain;

namespace LLMDesktopAssistant.LLM.Services
{
	/// <summary>
	/// Hosts the send pipeline: resolve → validate → insert → execute the command → hand off to generation.
	/// </summary>
	/// <remarks>
	/// Two entry points split by trust. <see cref="CanInsertUserInput"/> is a pure pre-flight the view model calls
	/// <em>before</em> it commits the draft, so a refused command never loses the user's text.
	/// <see cref="InsertUserInputAsync"/> is the authoritative path the chat-operations facade calls; it re-resolves, so
	/// a caller that skips the pre-flight (or a race with addon reloading) still gets a defined result — the message is
	/// inserted with the error attached and the command is not run.
	/// </remarks>
	public interface IChatMessageInsertionService
	{
		/// <summary>
		/// Checks whether <paramref name="input"/> can be inserted. Pure and side-effect free.
		/// </summary>
		/// <param name="input">The user input to check.</param>
		/// <param name="generateIntent">Whether the caller asked for generation.</param>
		/// <param name="editIndex">The edited message index, or <see langword="null"/> for a new message (edits never run a command).</param>
		UserInputInsertionCheckResult CanInsertUserInput(UserInput input, bool generateIntent, int? editIndex = null);

		/// <summary>
		/// Inserts <paramref name="input"/>, runs its command (if any) and hands off to generation.
		/// </summary>
		/// <param name="input">The user input to insert.</param>
		/// <param name="generateIntent">Whether the caller asked for generation; a command may only lower it.</param>
		/// <param name="editIndex">The edited message index, or <see langword="null"/> for a new message.</param>
		/// <param name="ct">The cancellation token (the caller's execution token).</param>
		Task InsertUserInputAsync(UserInput input, bool generateIntent, int? editIndex = null, CancellationToken ct = default);
	}
}
