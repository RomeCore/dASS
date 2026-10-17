using LLMDesktopAssistant.Tools.Execution.Domain;

namespace LLMDesktopAssistant.Tools.Execution.Services
{
	/// <summary>
	/// The preview stage: runs the descriptor's preview executor and folds its control outcome into the
	/// call. It also owns the call's expected behaviour (the descriptor default, overridden by preview).
	/// </summary>
	public interface IToolPreviewService
	{
		/// <summary>
		/// Runs the preview stage.
		/// </summary>
		/// <param name="ctx">The invocation.</param>
		/// <param name="ct">Cancellation of the run.</param>
		/// <returns><see langword="false"/> when the preview interrupted the invocation.</returns>
		Task<bool> RunAsync(IToolExecutionContext ctx, CancellationToken ct);
	}
}
