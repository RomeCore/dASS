using System.Text.Json.Nodes;

namespace LLMDesktopAssistant.Tools.Execution.Domain
{
	/// <summary>
	/// A tool's own approval interface. The confirmation service still owns the step; the provider only
	/// takes over the mechanics of the decision when the descriptor's
	/// <see cref="IToolDescriptor.HandledDecisions"/> mask allows it.
	/// </summary>
	public interface IToolApprovalProvider
	{
		/// <summary>
		/// The schema of the data the approval surface may request from the user (for example the set of
		/// enabled diff hunks), or <see langword="null"/> when the surface requests nothing.
		/// </summary>
		JsonObject? RequestedDataSchema { get; }

		/// <summary>
		/// Surfaces the tool's own approval interface and awaits the decision.
		/// </summary>
		/// <param name="ctx">The invocation being confirmed.</param>
		/// <param name="ct">Cancellation of the invocation.</param>
		/// <returns>The decision together with the memorization the user selected.</returns>
		Task<ToolApprovalResult> AwaitDecisionAsync(IToolExecutionContext ctx, CancellationToken ct);
	}
}
