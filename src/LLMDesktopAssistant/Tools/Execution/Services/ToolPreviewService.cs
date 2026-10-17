using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.Tools.Execution.Domain;
using Serilog;

namespace LLMDesktopAssistant.Tools.Execution.Services
{
	/// <inheritdoc cref="IToolPreviewService"/>
	[Service(typeof(IToolPreviewService))]
	public sealed class ToolPreviewService : IToolPreviewService
	{
		/// <inheritdoc/>
		public async Task<bool> RunAsync(IToolExecutionContext ctx, CancellationToken ct)
		{
			var preview = ctx.Descriptor.Preview;

			ctx.Call.ExpectedBehaviour = ctx.Descriptor.DefaultExpectedBehaviour;
			if (preview is null)
				return true;

			try
			{
				ctx.Call.Status = ToolStatus.PreExecuting;
				var result = await preview.PreviewAsync(ctx.Arguments.Effective, ctx, ct);

				if (result.ExpectedBehaviour is { } expectedBehaviour)
					ctx.Call.ExpectedBehaviour = expectedBehaviour;

				if (result.InterruptingSuccess is not { } success)
					return true;

				ctx.Call.Status = success ? ToolStatus.Success : ToolStatus.Error;
				if (string.IsNullOrEmpty(ctx.Call.ResultContent))
				{
					ctx.Call.ResultContent = success
						? "Tool successfully returned no result."
						: "Tool failed with no result.";
				}

				return false;
			}
			catch (ArgumentException ex)
			{
				ctx.Call.Status = ToolStatus.Error;
				ctx.Call.ResultContent = ex.Message;
				return false;
			}
			catch (Exception ex)
			{
				Log.Debug(ex, "Error during preview execution of tool '{ToolName}': {Error}", ctx.Call.ToolName, ex);
				return true;
			}
		}
	}
}
