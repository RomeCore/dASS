using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Tools.Execution.Services;

namespace LLMDesktopAssistant.Tools.Execution.Orchestrators
{
	/// <summary>
	/// The agent-task orchestrator: a tool the agent chose inside a task. The full pipeline runs with the
	/// task's confirmation host; streaming is absent because the task supplies complete arguments.
	/// </summary>
	/// <remarks>
	/// Written in P4, wired to <c>AgentTaskExecutor</c> in P5. It is Application-scoped, like every
	/// orchestrator: the task world's context factory, confirmation sink and host arrive through the
	/// dispatcher request.
	/// </remarks>
	[Service]
	public sealed class AgentTaskToolOrchestrator(
		IToolValidationService validation,
		IToolSynchronizationService synchronization,
		IToolPreviewService preview,
		IToolPolicyService policy,
		IToolConfirmationService confirmation,
		IToolExecutionService execution,
		IToolSettlementService settlement)
	{
		/// <summary>
		/// Runs one agent-task invocation.
		/// </summary>
		/// <param name="ctx">The invocation context.</param>
		/// <param name="ct">Cancellation of the task.</param>
		public async Task RunAsync(IToolExecutionContext ctx, CancellationToken ct)
		{
			try
			{
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
