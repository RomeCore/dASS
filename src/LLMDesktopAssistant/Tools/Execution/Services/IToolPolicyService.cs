using LLMDesktopAssistant.Tools.Execution.Domain;

namespace LLMDesktopAssistant.Tools.Execution.Services
{
	/// <summary>
	/// The policy stage: turns the descriptor metadata, the invocation's effective behaviours, the
	/// specifiers and the memorized decision into one decision with a reason.
	/// </summary>
	public interface IToolPolicyService
	{
		/// <summary>
		/// Evaluates the policy for one invocation and writes the decision and its reason onto the context.
		/// </summary>
		/// <param name="ctx">The invocation.</param>
		/// <param name="ct">Cancellation of the run.</param>
		/// <returns>The decision.</returns>
		Task<ToolPolicyDecision> EvaluateAsync(IToolExecutionContext ctx, CancellationToken ct);
	}
}
