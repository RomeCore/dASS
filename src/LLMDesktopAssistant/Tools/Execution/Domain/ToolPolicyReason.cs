namespace LLMDesktopAssistant.Tools.Execution.Domain
{
	/// <summary>
	/// Where a pipeline decision came from. A tool that handles a decision itself uses this to explain
	/// the decision to the user.
	/// </summary>
	public enum ToolDecisionSource
	{
		/// <summary>
		/// The tool's own approval level.
		/// </summary>
		ApprovalLevel,

		/// <summary>
		/// The tool's policy mask overrode the agent policy.
		/// </summary>
		PolicyMask,

		/// <summary>
		/// A specifier rule produced the decision.
		/// </summary>
		Specifier,

		/// <summary>
		/// A memorized user decision produced the decision.
		/// </summary>
		Memorization,

		/// <summary>
		/// The user decided during this invocation.
		/// </summary>
		User
	}

	/// <summary>
	/// A pipeline decision together with a message explaining it. Carried on the execution context so
	/// a tool that handles the decision itself can render the reason instead of inventing one.
	/// </summary>
	/// <param name="Source">The layer that produced the decision.</param>
	/// <param name="Message">A human-readable explanation, or <see langword="null"/>.</param>
	public readonly record struct ToolPolicyReason(ToolDecisionSource Source, string? Message);
}
