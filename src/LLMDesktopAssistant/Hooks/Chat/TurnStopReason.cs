namespace LLMDesktopAssistant.Hooks.Chat
{
	/// <summary>
	/// Why the multi-agent turn ended, carried as the variant of <c>chat.turn.stop.*</c>.
	/// </summary>
	public enum TurnStopReason
	{
		/// <summary>
		/// No agent was selected and nothing is pending: the turn has nothing left to do.
		/// </summary>
		Complete,

		/// <summary>
		/// A follow-up message the user wrote during the turn is pending, so the turn ends and the next one
		/// picks it up.
		/// </summary>
		Interrupted,

		/// <summary>
		/// The turn was cancelled before it finished.
		/// </summary>
		Cancelled
	}
}
