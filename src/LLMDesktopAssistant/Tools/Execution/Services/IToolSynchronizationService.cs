using LLMDesktopAssistant.Tools.Execution.Domain;

namespace LLMDesktopAssistant.Tools.Execution.Services
{
	/// <summary>
	/// The synchronization stage: a keyed higher-order scope around the inner steps of an invocation.
	/// Every lock in the tool pipeline lives here.
	/// </summary>
	public interface IToolSynchronizationService
	{
		/// <summary>
		/// Acquires the invocation's whole key set atomically, runs the inner steps and releases the keys.
		/// </summary>
		/// <param name="ctx">The invocation.</param>
		/// <param name="inner">The steps protected by the key set.</param>
		/// <param name="ct">Cancellation of the run; cancels the wait for the keys.</param>
		Task RunAsync(IToolExecutionContext ctx, Func<Task> inner, CancellationToken ct);
	}
}
