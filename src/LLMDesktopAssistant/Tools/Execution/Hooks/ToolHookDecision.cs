namespace LLMDesktopAssistant.Tools.Execution.Hooks
{
	/// <summary>
	/// The opinion a tool hook expresses about an invocation.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A plain enum on purpose: a hook answers with one value, and there is no combination for the pipeline
	/// to interpret or to warn about. An unknown word fails deserialization and means "no opinion".
	/// </para>
	/// <para>
	/// <see cref="Allow"/> and <see cref="Ask"/> are the same thing where the point runs before the policy
	/// stage: neither can lift a decision that has not been made yet, so both mean "no objection". The two
	/// names exist because <see cref="Ask"/> becomes a real request at a point that runs after the policy,
	/// and a hook authored against that point should not have to be rewritten.
	/// </para>
	/// </remarks>
	public enum ToolHookDecision
	{
		/// <summary>
		/// No opinion. The same as returning no verdict at all.
		/// </summary>
		None,

		/// <summary>
		/// No objection.
		/// </summary>
		Allow,

		/// <summary>
		/// No objection here: the call is left to the policy stage and, through it, to the user.
		/// </summary>
		Ask,

		/// <summary>
		/// A refusal. The call never runs and its status becomes
		/// <see cref="LLM.Domain.ToolStatus.Rejected"/>: the call was unwanted, not broken.
		/// </summary>
		Disallow,

		/// <summary>
		/// A refusal on the grounds that the call cannot be carried out - the arguments are wrong, a
		/// precondition does not hold, the guard itself could not decide. The status becomes
		/// <see cref="LLM.Domain.ToolStatus.Error"/>, which tells the user the call failed rather than that it
		/// was turned down.
		/// </summary>
		Error
	}
}
