using LLMDesktopAssistant.Tools.Execution.Domain;

namespace LLMDesktopAssistant.Tools.Execution.Services
{
	/// <summary>
	/// The settlement stage: finalizes a call. It always runs in the caller's <c>finally</c>, including
	/// after a cancellation or an exception.
	/// </summary>
	public interface IToolSettlementService
	{
		/// <summary>
		/// Finalizes the call: fills the default result content when the tool produced none, based on the
		/// terminal status.
		/// </summary>
		/// <param name="ctx">The invocation.</param>
		void Finalize(IToolExecutionContext ctx);
	}
}
