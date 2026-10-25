using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Tools.Execution.Services;

namespace LLMDesktopAssistant.Tools.Execution.Orchestrators
{
	/// <summary>
	/// The chat-run orchestrator: the full pipeline for a tool the agent chose during a chat turn.
	/// </summary>
	/// <remarks>
	/// The sequence is explicit imperative code on purpose - no stage engine, no shared inner helper. The
	/// only shared pieces are the stage services and the synchronization higher-order wrapper. The call's
	/// terminal state belongs to the settlement stage, which runs at the end of the sequence or from the
	/// catch block; there is no <c>finally</c>.
	/// </remarks>
	[Service]
	public sealed class ChatToolOrchestrator(
		IToolInitializationService initialization,
		IToolStreamingService streaming,
		IToolValidationService validation,
		IToolSynchronizationService synchronization,
		IToolPreviewService preview,
		IToolPolicyService policy,
		IToolConfirmationService confirmation,
		IToolExecutionService execution,
		IToolSettlementService settlement)
	{
		/// <summary>
		/// Runs one chat-run invocation.
		/// </summary>
		/// <param name="ctx">The invocation context.</param>
		/// <param name="ct">Cancellation of the run.</param>
		public async Task RunAsync(IToolExecutionContext ctx, CancellationToken ct)
		{
			try
			{
				initialization.Initialize(ctx, ref ct);
				await streaming.RunAsync(ctx, StreamingMode.Cyclic, ct);
				validation.Validate(ctx);

				await synchronization.RunAsync(ctx, async () =>
				{
					if (!await preview.RunAsync(ctx, ct))
						return;

					await policy.EvaluateAsync(ctx, ct);

					if (!await confirmation.ConfirmAsync(ctx, ct))
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
