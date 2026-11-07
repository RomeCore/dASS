using LLMDesktopAssistant.Tools.Execution.Domain;

namespace LLMDesktopAssistant.Tools.Execution.Services
{
	/// <summary>
	/// The hook point that answers the confirmation question in the user's place.
	/// </summary>
	public interface IToolConfirmationHookExecutionService
	{
		/// <summary>
		/// Fires <c>tool.confirm.{source}.{pending}</c> for one invocation that reached the confirmation
		/// stage.
		/// </summary>
		/// <remarks>
		/// A refusal is final; an approval or a request to ask leaves the rest of the stage to run.
		/// </remarks>
		/// <param name="ctx">The invocation context.</param>
		/// <param name="ct">Cancellation of the run.</param>
		/// <returns>
		/// <see langword="false"/> when a hook refused the call, so the invocation must not execute.
		/// </returns>
		Task<bool> RunAsync(IToolExecutionContext ctx, CancellationToken ct = default);
	}
}
