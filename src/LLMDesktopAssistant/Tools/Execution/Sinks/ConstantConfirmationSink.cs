using LLMDesktopAssistant.Tools.Execution.Domain;

namespace LLMDesktopAssistant.Tools.Execution.Sinks
{
	/// <summary>
	/// A confirmation surface that always answers the same way. It is used where the confirmation step
	/// cannot run by design (a script invocation, a user action), and as a safe stand-in where an
	/// invocation must never block on a panel nobody will resolve.
	/// </summary>
	public sealed class ConstantConfirmationSink(ToolApprovalResult result) : IConfirmationSink
	{
		/// <summary>
		/// Approves every request without remembering the decision.
		/// </summary>
		public static ConstantConfirmationSink Approve { get; } = new(ToolApprovalResult.ApprovedOnce());

		/// <summary>
		/// Denies every request without remembering the decision.
		/// </summary>
		public static ConstantConfirmationSink Disallow { get; } =
			new(ToolApprovalResult.DeniedOnce("Confirmation is unavailable for this invocation."));

		/// <inheritdoc/>
		public Task<ToolApprovalResult> RequestAsync(IToolExecutionContext ctx, CancellationToken ct)
			=> Task.FromResult(result);
	}
}
