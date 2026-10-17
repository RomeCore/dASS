namespace LLMDesktopAssistant.Tools.Execution.Domain
{
	/// <summary>
	/// Shows a confirmation to the user and returns the decision. Each world provides its own
	/// implementation: the chat sink drives the consent panel through the call's confirmation source,
	/// the task sink drives the task card through its confirmation requests.
	/// </summary>
	/// <remarks>
	/// The confirmation stage stays world-agnostic by talking to this port instead of branching on
	/// <see cref="IToolExecutionContext.Source"/>.
	/// </remarks>
	public interface IConfirmationSink
	{
		/// <summary>
		/// Surfaces the confirmation and awaits the user's decision.
		/// </summary>
		/// <param name="ctx">The invocation being confirmed.</param>
		/// <param name="ct">Cancellation of the invocation.</param>
		/// <returns>The decision together with the memorization the user selected.</returns>
		Task<ToolApprovalResult> RequestAsync(IToolExecutionContext ctx, CancellationToken ct);
	}
}
