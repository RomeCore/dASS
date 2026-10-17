using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.Tools.Execution.Domain;

namespace LLMDesktopAssistant.Tools.Execution.Services
{
	/// <summary>
	/// The settlement stage. The completion state is the call's status, owned by the execution stage (and
	/// by the caller's cancellation/error handling); settlement only makes sure the model sees something.
	/// </summary>
	[Service(typeof(IToolSettlementService))]
	public sealed class ToolSettlementService : IToolSettlementService
	{
		/// <inheritdoc/>
		public void Finalize(IToolExecutionContext ctx)
		{
			var call = ctx.Call;
			if (!string.IsNullOrEmpty(call.ResultContent))
				return;

			call.ResultContent = call.Status switch
			{
				ToolStatus.Cancelled => "Tool execution was cancelled.",
				ToolStatus.Success => "Tool successfully returned no result.",
				_ => "Tool failed with no result."
			};
		}
	}
}
