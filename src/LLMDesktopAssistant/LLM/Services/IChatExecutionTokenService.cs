namespace LLMDesktopAssistant.LLM.Services
{
	/// <summary>
	/// Owns cancellation for every chat execution level and replaces the previous ad-hoc cancellation plumbing.
	/// </summary>
	public interface IChatExecutionTokenService
	{
		/// <summary>
		/// Gets the token that covers every level: it is alive (<see langword="null"/> when the chat is idle) while
		/// at least one level is active, and cancelling it cancels every level at once.
		/// This is the single source the UI uses to know that the chat is busy and to stop it.
		/// </summary>
		CancellationTokenSource? ExecutionCancellationToken { get; }

		/// <summary>
		/// Raised when <see cref="ExecutionCancellationToken"/> appears (the first level became active) or disappears
		/// (the last level was released). Raised on the calling thread.
		/// </summary>
		event Action? ExecutionCancellationTokenChanged;

		/// <summary>
		/// Takes the specified level and returns a handle that releases it. Taking a level cancels/replaces the token
		/// of that level and cascades into every narrower level. Levels may be skipped.
		/// </summary>
		/// <param name="level">The level to take. Must not be <see cref="ChatExecutionLevel.None"/>.</param>
		/// <param name="inputCt">An optional external token to link the level's token to.</param>
		/// <param name="cancellationToken">The cancellation token of the taken level.</param>
		/// <returns>A handle that releases the level; disposing it cascades into the narrower levels.</returns>
		IDisposable WithToken(ChatExecutionLevel level, CancellationToken inputCt, out CancellationToken cancellationToken);

		/// <summary>
		/// Cancels the token of the specified level, cascading into every narrower level.
		/// </summary>
		/// <param name="level">The level to cancel.</param>
		/// <returns><see langword="true"/> if the level was active and got cancelled; otherwise <see langword="false"/>.</returns>
		bool TryCancel(ChatExecutionLevel level);
	}
}
