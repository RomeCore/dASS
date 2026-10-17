using LLMDesktopAssistant.Tools.Execution.Domain;

namespace LLMDesktopAssistant.Tools.Execution.Services
{
	/// <summary>
	/// The execution stage: runs the descriptor's main executor and folds its result into the call.
	/// </summary>
	public interface IToolExecutionService
	{
		/// <summary>
		/// Runs the main executor for one invocation.
		/// </summary>
		/// <param name="ctx">The invocation.</param>
		/// <param name="ct">Cancellation of the run.</param>
		Task RunAsync(IToolExecutionContext ctx, CancellationToken ct);
	}
}
