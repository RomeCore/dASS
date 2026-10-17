using LLMDesktopAssistant.Tools.Execution.Domain;

namespace LLMDesktopAssistant.Tools.Execution.Services
{
	/// <summary>
	/// The confirmation stage: asks the user when the policy says so, memorizes the decision and folds
	/// the refusal (or the user's notes) into the call.
	/// </summary>
	public interface IToolConfirmationService
	{
		/// <summary>
		/// Runs the confirmation stage for one invocation.
		/// </summary>
		/// <param name="ctx">The invocation.</param>
		/// <param name="ct">Cancellation of the run.</param>
		/// <returns><see langword="true"/> when the invocation may execute.</returns>
		Task<bool> ConfirmAsync(IToolExecutionContext ctx, CancellationToken ct);
	}
}
