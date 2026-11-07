using LLMDesktopAssistant.Tools.Execution.Domain;

namespace LLMDesktopAssistant.Tools.Execution.Services
{
	/// <summary>
	/// The hook point that runs once a call has its final result.
	/// </summary>
	public interface IToolResultHookExecutionService
	{
		/// <summary>
		/// Fires <c>tool.result.{source}.{status}</c> for one settled invocation.
		/// </summary>
		/// <remarks>
		/// The event is composed from the call's own status, so the hook author selects the outcomes with a
		/// glob instead of branching in the script. A hook may replace the text the model reads; the status
		/// and the structured result stay final.
		/// </remarks>
		/// <param name="ctx">The invocation context.</param>
		/// <param name="exception">The exception the call ended with, or <see langword="null"/>.</param>
		/// <param name="ct">Cancellation of the run.</param>
		Task RunAsync(IToolExecutionContext ctx, Exception? exception, CancellationToken ct = default);
	}
}
