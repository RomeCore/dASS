using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.Tools.Consents;
using LLMDesktopAssistant.Tools.Execution.Domain;

namespace LLMDesktopAssistant.Tools.Execution.Sinks
{
	/// <summary>
	/// The chat world's confirmation surface. It creates the call's confirmation source, marks the call
	/// as awaiting approval and waits for the consent panel to resolve it.
	/// </summary>
	/// <remarks>
	/// The sink knows nothing about the pipeline: it only turns the call's runtime confirmation channel
	/// into a <see cref="ToolApprovalResult"/>. The "always" persistence belongs to the confirmation
	/// stage, which owns the sender agent and the settings.
	/// </remarks>
	public sealed class ChatConfirmationSink(IChatExecutionStatusService statusService) : IConfirmationSink
	{
		/// <summary>
		/// Surfaces the confirmation and awaits the user's decision.
		/// </summary>
		/// <param name="ctx">The invocation being confirmed.</param>
		/// <param name="ct">Cancellation of the invocation.</param>
		/// <returns>The decision together with the memorization the user selected.</returns>
		public async Task<ToolApprovalResult> RequestAsync(IToolExecutionContext ctx, CancellationToken ct)
		{
			using var confirmation = statusService.WithConfirmation();

			var source = new TaskCompletionSource<ToolConsentResult>(TaskCreationOptions.RunContinuationsAsynchronously);
			ctx.Call.UserConfirmationSource = source;
			ctx.Call.Status = ToolStatus.WaitingForApproval;

			var consent = await source.Task.WaitAsync(ct);
			return new ToolApprovalResult
			{
				Approved = consent.IsApproved,
				Memorization = consent.Memorization,
				Notes = consent.Notes
			};
		}
	}
}
