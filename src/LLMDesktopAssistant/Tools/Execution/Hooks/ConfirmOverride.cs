namespace LLMDesktopAssistant.Tools.Execution.Hooks
{
	/// <summary>
	/// The deepest layer a confirmation hook may override, ordered by strictness.
	/// </summary>
	/// <remarks>
	/// <see cref="Policy"/> is the default, so a generic auto-approver cannot undo a memorized "no" or a
	/// tool author's unconditional refusal by accident.
	/// </remarks>
	public enum ConfirmOverride
	{
		/// <summary>
		/// The agent's policy, its specifiers and the tool's own approval level.
		/// </summary>
		Policy,

		/// <summary>
		/// The same, plus a decision the user memorized for the session or the task.
		/// </summary>
		Memorization,

		/// <summary>
		/// Every layer, including <see cref="LLMDesktopAssistant.Tools.ToolApprovalLevel.AlwaysDisallow"/>.
		/// </summary>
		All
	}
}
