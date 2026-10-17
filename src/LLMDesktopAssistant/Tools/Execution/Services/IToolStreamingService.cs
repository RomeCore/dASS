using LLMDesktopAssistant.Tools.Execution.Domain;

namespace LLMDesktopAssistant.Tools.Execution.Services
{
	/// <summary>
	/// The streaming stage: drains the neutral argument stream, keeps <c>call.Arguments</c> in sync and
	/// runs the optional streaming analyzer.
	/// </summary>
	public interface IToolStreamingService
	{
		/// <summary>
		/// Runs the streaming stage for one invocation.
		/// </summary>
		/// <param name="ctx">The invocation.</param>
		/// <param name="mode">Whether the analyzer runs on every update or once over the complete set.</param>
		/// <param name="ct">Cancellation of the run.</param>
		Task RunAsync(IToolExecutionContext ctx, StreamingMode mode, CancellationToken ct);
	}
}
