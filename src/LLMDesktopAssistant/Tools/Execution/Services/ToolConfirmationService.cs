using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.MVVM.Additional;
using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.Tools.Consents;
using LLMDesktopAssistant.Tools.Execution.Domain;

namespace LLMDesktopAssistant.Tools.Execution.Services
{
	/// <summary>
	/// The confirmation stage. It reads the decision from the context and the surface from
	/// <see cref="IToolExecutionContext.Confirmation"/>; it owns no state of its own.
	/// </summary>
	/// <remarks>
	/// The descriptor's own approval provider (the <see cref="IToolDescriptor.HandledDecisions"/> split)
	/// lands in P6; this implementation keeps the pre-existing semantics: a handled decision is left to the
	/// tool, an unhandled one is served by the generic surface.
	/// </remarks>
	[Service(typeof(IToolConfirmationService))]
	public sealed class ToolConfirmationService : IToolConfirmationService
	{
		/// <inheritdoc/>
		public async Task<bool> ConfirmAsync(IToolExecutionContext ctx, CancellationToken ct)
		{
			var decision = ctx.PolicyDecision;
			var handled = ctx.Descriptor.HandledDecisions;

			if (decision == ToolPolicyDecision.Disallow && !handled.HasFlag(ToolPolicyDecision.Disallow))
			{
				ctx.Call.Status = ToolStatus.Error;
				ctx.Call.ResultContent = ctx.PolicyReason?.Message ??
					"The tool execution was denied by the policy.";
				return false;
			}

			if (decision != ToolPolicyDecision.Ask || handled.HasFlag(ToolPolicyDecision.Ask))
				return true;

			var approval = await ctx.Confirmation.RequestAsync(ctx, ct);
			Memorize(ctx, approval);

			if (!approval.Approved)
			{
				ctx.Call.Status = ToolStatus.Cancelled;
				ctx.Call.ResultContent = string.IsNullOrWhiteSpace(approval.Notes)
					? "User has cancelled the tool execution without a reason. " +
					"Maybe it can be dangerous or unwanted to proceed."
					: $"User has cancelled the tool execution with a reason: {approval.Notes}.";
				return false;
			}

			AppendNotes(ctx, approval.Notes);
			return true;
		}

		private static void Memorize(IToolExecutionContext ctx, ToolApprovalResult approval)
		{
			ctx.Consent?.Memorize(new ToolConsentResult
			{
				IsApproved = approval.Approved,
				Memorization = approval.Memorization,
				Notes = approval.Notes
			});
		}

		/// <summary>
		/// The user's notes travel as an invisible content part on the call; the prompt layer appends it to
		/// the tool result. The part is persisted with the call, so a reload keeps it in the prompt.
		/// </summary>
		private static void AppendNotes(IToolExecutionContext ctx, string? notes)
		{
			if (string.IsNullOrWhiteSpace(notes))
				return;

			ctx.Call.AddAdditionalData(new AdditionalMessageContentPart
			{
				Content = $"Additional notes from user: {notes}",
				IsVisible = false,
				IsRestorable = false
			});
		}
	}
}
