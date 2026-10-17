using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Utils;
using Serilog;

namespace LLMDesktopAssistant.Tools.Execution.Services
{
	/// <summary>
	/// The settlement stage. The completion state is the call's status, owned by the execution stage (and
	/// by this stage when the sequence threw); settlement classifies a thrown exception and makes sure the
	/// model sees something.
	/// </summary>
	/// <remarks>
	/// Cancellation is a normal outcome, not an error: the tool runs next to its siblings and the run must
	/// survive one call being cancelled. Everything else becomes <see cref="ToolStatus.Error"/>; a
	/// <see cref="ToolInvocationException"/> carries a model-facing message and is written verbatim, while
	/// an unexpected exception is prefixed and its stack trace appears only in debug builds.
	/// </remarks>
	[Service(typeof(IToolSettlementService))]
	public sealed class ToolSettlementService : IToolSettlementService
	{
		/// <inheritdoc/>
		public void Finalize(IToolExecutionContext ctx, Exception? exception)
		{
			var call = ctx.Call;

			if (exception is not null)
				Apply(ctx, exception);

			if (!string.IsNullOrEmpty(call.ResultContent))
				return;

			call.ResultContent = call.Status switch
			{
				ToolStatus.Cancelled => "Tool execution was cancelled.",
				ToolStatus.Success => "Tool successfully returned no result.",
				_ => "Tool failed with no result."
			};
		}

		private static void Apply(IToolExecutionContext ctx, Exception exception)
		{
			var cancelled = IsCancellation(exception);
			ctx.Call.Status = cancelled ? ToolStatus.Cancelled : ToolStatus.Error;

			if (!cancelled)
				Log.Debug(exception, "Unhandled error during execution of tool '{ToolName}'.", ctx.Call.ToolName);

			var text = cancelled
				? "Tool execution was interrupted."
				: exception is ToolInvocationException
					? exception.Message
					: "Tool execution failed with error: " +
					(DebugHelper.IsDebug ? exception.ToString() : exception.Message);

			Append(ctx.Call, text);
		}

		private static bool IsCancellation(Exception exception) =>
			exception is OperationCanceledException
			|| exception is AggregateException aggregate
			&& aggregate.InnerExceptions.Any(e => e is OperationCanceledException);

		private static void Append(IToolCall call, string text) =>
			call.ResultContent = string.IsNullOrEmpty(call.ResultContent)
				? text
				: call.ResultContent + "\n" + text;
	}
}
