using System.Text.Json.Nodes;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Utils;
using Serilog;

namespace LLMDesktopAssistant.Tools.Execution.Services
{
	/// <summary>
	/// Drains the argument stream and feeds the analyzer. Stateless: everything lives on the context.
	/// </summary>
	[Service(typeof(IToolStreamingService))]
	public sealed class ToolStreamingService : IToolStreamingService
	{
		/// <inheritdoc/>
		public async Task RunAsync(IToolExecutionContext ctx, StreamingMode mode, CancellationToken ct)
		{
			var stream = ctx.ArgumentStream;
			if (stream is null)
			{
				// A restart (or a non-streaming provider) has no stream; `Once` still analyses the complete set.
				if (mode == StreamingMode.Once)
					AnalyzeComplete(ctx);

				ct.ThrowIfCancellationRequested();
				return;
			}

			ctx.Call.Status = ToolStatus.Pending;

			switch (mode)
			{
				case StreamingMode.Once:
					await stream.Drained.WaitAsync(ct);
					if (!string.IsNullOrEmpty(stream.AccumulatedArgs))
						ctx.Call.Arguments = stream.AccumulatedArgs;

					AnalyzeComplete(ctx);
					break;

				case StreamingMode.Cyclic:
				default:
					await RunCyclicAsync(ctx, stream, ct);
					break;
			}

			ct.ThrowIfCancellationRequested();
		}

		private static async Task RunCyclicAsync(IToolExecutionContext ctx, IToolArgumentStream stream,
			CancellationToken ct)
		{
			var analyzer = ctx.Descriptor.Streaming;

			void OnUpdated()
			{
				ctx.Call.Arguments = stream.AccumulatedArgs;
				if (analyzer is null)
					return;

				try
				{
					// TolerantJsonParser can parse partial (unfinished) JSON too.
					var args = TolerantJsonParser.Parse(ctx.Call.Arguments) ?? new JsonObject();
					if (analyzer.AnalyzeArgs(args, ctx).StopAnalysis)
						analyzer = null;
				}
				catch (Exception ex)
				{
					// A streaming analyzer must never break the run because of one bad update.
					Log.Debug(ex, "Streaming analysis failed for tool '{ToolName}'.", ctx.Call.ToolName);
				}
			}

			using var subscription = stream.OnArgsUpdated(OnUpdated);
			await stream.Drained.WaitAsync(ct);
		}

		private static void AnalyzeComplete(IToolExecutionContext ctx)
		{
			var analyzer = ctx.Descriptor.Streaming;
			if (analyzer is null)
				return;

			try
			{
				analyzer.AnalyzeArgs(ctx.Arguments.Effective ?? new JsonObject(), ctx);
			}
			catch (Exception ex)
			{
				Log.Debug(ex, "Streaming analysis failed for tool '{ToolName}'.", ctx.Call.ToolName);
			}
		}
	}
}
