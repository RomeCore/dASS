using LLMDesktopAssistant.Tools.Execution.Domain;

namespace LLMDesktopAssistant.Tools.Execution.Services
{
	/// <summary>
	/// The settlement stage: finalizes a call. It is the single owner of the call's terminal state — an
	/// orchestrator calls it at the end of its sequence or from its catch block, and there is no
	/// <c>finally</c>.
	/// </summary>
	public interface IToolSettlementService
	{
		/// <summary>
		/// Finalizes the call: applies the failure (when one was thrown), fills the default result content
		/// when the tool produced none, based on the terminal status.
		/// </summary>
		/// <param name="ctx">The invocation.</param>
		/// <param name="exception">
		/// The exception that interrupted the sequence, or <see langword="null"/> when it ran to the end.
		/// The parameter has no default on purpose: every call site states which outcome it is settling.
		/// </param>
		void Finalize(IToolExecutionContext ctx, Exception? exception);
	}
}
