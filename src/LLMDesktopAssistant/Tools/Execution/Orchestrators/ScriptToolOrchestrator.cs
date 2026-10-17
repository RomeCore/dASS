using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Tools.Execution.Services;

namespace LLMDesktopAssistant.Tools.Execution.Orchestrators
{
	/// <summary>
	/// The script orchestrator: a nested call made by Lua/CSX through <c>dass.tool.call</c>. Scripts are a
	/// deliberately powerful area: no policy, no confirmation, no hard floor.
	/// </summary>
	/// <remarks>
	/// There is no streaming stage (a script supplies complete arguments) and no synchronization stage.
	/// The missing synchronization is the honest gap: sync keys are not reentrant, so a tool holding a key
	/// whose script calls another tool under the same key would wait on itself forever.
	/// </remarks>
	[Service]
	public sealed class ScriptToolOrchestrator(
		IToolValidationService validation,
		IToolPreviewService preview,
		IToolExecutionService execution,
		IToolSettlementService settlement)
	{
		/// <summary>
		/// Runs one script invocation.
		/// </summary>
		/// <param name="ctx">The invocation context.</param>
		/// <param name="ct">Cancellation of the call.</param>
		public async Task RunAsync(IToolExecutionContext ctx, CancellationToken ct)
		{
			try
			{
				validation.Validate(ctx);

				if (await preview.RunAsync(ctx, ct))
					await execution.RunAsync(ctx, ct);

				settlement.Finalize(ctx, null);
			}
			catch (Exception ex)
			{
				settlement.Finalize(ctx, ex);
			}

			// TODO(script-sync): the synchronization stage is skipped on purpose. Sync keys are not
			// reentrant: a tool holding AllFiles (or a directory) that calls another tool through
			// `dass.tool.call` would wait forever on the keys its own caller holds. Decide between
			// key-reentrancy tracking and a narrower skip rule. Tracked in not-implemented.md.
		}
	}
}
