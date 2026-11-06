using LLMDesktopAssistant.Tools.Execution.Domain;

namespace LLMDesktopAssistant.Tools.Execution.Services
{
	/// <summary>
	/// The hook point that runs before an invocation is validated and locked.
	/// </summary>
	public interface IToolPreHookExecutionService
	{
		/// <summary>
		/// Fires the <c>tool.pre-use</c> hooks for one invocation.
		/// </summary>
		/// <remarks>
		/// A hook may veto the invocation - the call's status and the model-facing text are written here -
		/// or replace the effective argument set, which every later stage then sees.
		/// </remarks>
		/// <param name="ctx">The invocation context.</param>
		/// <param name="ct">Cancellation of the run.</param>
		/// <returns>
		/// <see langword="false"/> when a hook vetoed the invocation and the rest of the pipeline must be
		/// skipped; <see langword="true"/> otherwise.
		/// </returns>
		Task<bool> RunAsync(IToolExecutionContext ctx, CancellationToken ct);
	}
}
