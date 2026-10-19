using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Tools.Execution.Services;

namespace LLMDesktopAssistant.Tools.Execution.Orchestrators
{
	/// <summary>
	/// The tool-command orchestrator: the user runs the tool through its derived slash command, so policy
	/// and confirmation are skipped and the hard floor is not checked — the action is the user's own.
	/// </summary>
	/// <remarks>
	/// The body is identical to <see cref="ChatRestartOrchestrator"/> by design: the sources are different
	/// entry points and stay separate orchestrators instead of a shared helper.
	/// </remarks>
	[Service]
	public sealed class ToolCommandOrchestrator(
		IToolInitializationService initialization,
		IToolStreamingService streaming,
		IToolValidationService validation,
		IToolSynchronizationService synchronization,
		IToolPreviewService preview,
		IToolExecutionService execution,
		IToolSettlementService settlement)
	{
		/// <summary>
		/// Runs one tool-command invocation.
		/// </summary>
		/// <param name="ctx">The invocation context.</param>
		/// <param name="ct">Cancellation of the run.</param>
		public async Task RunAsync(IToolExecutionContext ctx, CancellationToken ct)
		{
			try
			{
				initialization.Initialize(ctx, ref ct);
				await streaming.RunAsync(ctx, StreamingMode.Once, ct);
				validation.Validate(ctx);

				await synchronization.RunAsync(ctx, async () =>
				{
					if (!await preview.RunAsync(ctx, ct))
						return;

					await execution.RunAsync(ctx, ct);
				}, ct);

				settlement.Finalize(ctx, null);
			}
			catch (Exception ex)
			{
				settlement.Finalize(ctx, ex);
			}
		}
	}
}
