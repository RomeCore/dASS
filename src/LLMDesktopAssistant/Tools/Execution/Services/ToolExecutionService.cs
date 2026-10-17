using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.Tools.Execution.Domain;

namespace LLMDesktopAssistant.Tools.Execution.Services
{
	/// <summary>
	/// The execution stage. The executor's task <em>is</em> the completion: its result is folded into the
	/// call, and the call's status becomes the completion state.
	/// </summary>
	[Service(typeof(IToolExecutionService))]
	public sealed class ToolExecutionService : IToolExecutionService
	{
		/// <inheritdoc/>
		public async Task RunAsync(IToolExecutionContext ctx, CancellationToken ct)
		{
			ctx.Call.Status = ToolStatus.Executing;
			ct.ThrowIfCancellationRequested();

			var result = await ctx.Descriptor.Main.ExecuteAsync(ctx.Arguments.Effective, ctx, ct);

			ctx.Call.Status = ct.IsCancellationRequested
				? ToolStatus.Cancelled
				: result.Success ? ToolStatus.Success : ToolStatus.Error;
		}
	}
}
