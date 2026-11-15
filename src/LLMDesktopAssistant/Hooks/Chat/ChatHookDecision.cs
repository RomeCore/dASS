namespace LLMDesktopAssistant.Hooks.Chat
{
	/// <summary>
	/// What a gating chat hook answers about the moment it was asked about.
	/// </summary>
	/// <remarks>
	/// Deliberately separate from the tool pipeline's decision: a message is not an invocation, and the vocabulary
	/// there - allow, ask, disallow, error - carries an approval stage this pipeline does not have.
	/// </remarks>
	public enum ChatHookDecision
	{
		/// <summary>
		/// No objection: the pipeline continues exactly as it would without the hook.
		/// </summary>
		Allow,

		/// <summary>
		/// A refusal: the send, the write or the turn does not happen.
		/// </summary>
		Disallow,

		/// <summary>
		/// The hook could not answer, which refuses the step as well but is reported as a failure, not as a decision.
		/// </summary>
		Error
	}
}
