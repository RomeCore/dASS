namespace LLMDesktopAssistant.Tools.Execution.Hooks
{
	/// <summary>
	/// The opinion a tool hook expresses about an invocation.
	/// </summary>
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
