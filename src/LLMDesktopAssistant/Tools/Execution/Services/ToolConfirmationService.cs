using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.MVVM.Additional;
using LLMDesktopAssistant.LLM.Services;
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
	/// A decision the tool handles itself is delegated to <see cref="IToolDescriptor.Approval"/>; the
	/// per-invocation mask comes from <see cref="IToolExecutionContext.HandledDecisions"/> so preview can
	/// narrow or widen it. A handled decision without a provider fails the invocation: silently falling
	/// back to the generic surface would execute something the tool never agreed to surface.
	/// </remarks>
	[Service(typeof(IToolConfirmationService))]
	public sealed class ToolConfirmationService : IToolConfirmationService
	{
		/// <inheritdoc/>
		public async Task<bool> ConfirmAsync(IToolExecutionContext ctx, CancellationToken ct)
		{
			var decision = ctx.PolicyDecision;
			var handled = ctx.HandledDecisions;

			if (decision == ToolPolicyDecision.Disallow)
			{
				if (handled.HasFlag(ToolPolicyDecision.Disallow))
					return await AskToolAsync(ctx, ct);

				ctx.Call.Status = ToolStatus.Error;
				ctx.Call.ResultContent = ctx.PolicyReason?.Message ??
					"The tool execution was denied by the policy.";
				return false;
			}

			if (decision != ToolPolicyDecision.Ask)
				return true;

			ctx.Call.Status = ToolStatus.WaitingForApproval;
			using var confirmation = ctx.Services.GetService<IChatExecutionStatusService>()?.WithConfirmation();

			if (handled.HasFlag(ToolPolicyDecision.Ask))
				return await AskToolAsync(ctx, ct);

			return Apply(ctx, await ctx.Confirmation.RequestAsync(ctx, ct));
		}

		/// <summary>
		/// Delegates the decision to the tool's own approval provider. A provider-less handled decision is a
		/// descriptor violation (preview may have widened the mask after construction) and fails the call.
		/// </summary>
		private async Task<bool> AskToolAsync(IToolExecutionContext ctx, CancellationToken ct)
		{
			var provider = ctx.Descriptor.Approval;
			if (provider is null)
			{
				ctx.Call.Status = ToolStatus.Error;
				ctx.Call.ResultContent =
					$"Tool '{ctx.Call.ToolName}' declared a self-handled decision but provides no approval provider.";
				return false;
			}

			return Apply(ctx, await provider.AwaitDecisionAsync(ctx, ct));
		}

		/// <summary>
		/// Applies the decision: memorizes it, writes the cancellation or appends the user notes.
		/// </summary>
		private bool Apply(IToolExecutionContext ctx, ToolApprovalResult approval)
		{
			if (!approval.Approved)
			{
				Memorize(ctx, approval);
				ctx.Call.Status = ToolStatus.Cancelled;
				ctx.Call.ResultContent = string.IsNullOrWhiteSpace(approval.Notes)
					? "User has cancelled the tool execution without a reason. " +
					"Maybe it can be dangerous or unwanted to proceed."
					: $"User has cancelled the tool execution with a reason: {approval.Notes}.";
				return false;
			}

			if (approval.ApprovalData is { } approvalData)
				ctx.ApprovalData = approvalData;

			if (approval.EditedArguments is { } edited)
			{
				if (approval.OverrideModelIntent)
				{
					ctx.Call.Arguments = edited.ToJsonString();
					ctx.ArgumentsOverride = null;
				}
				else
				{
					ctx.ArgumentsOverride = edited;
				}
			}

			Memorize(ctx, approval);
			AppendNotes(ctx, approval.Notes);
			return true;
		}

		private static void Memorize(IToolExecutionContext ctx, ToolApprovalResult approval)
		{
			ctx.Consent?.Memorize(approval);
		}

		/// <summary>
		/// The user's notes travel as an invisible content part on the call; the prompt layer appends it to
		/// the tool result. The part is persisted with the call, so a reload keeps it in the prompt.
		/// </summary>
		private static void AppendNotes(IToolExecutionContext ctx, string? notes)
		{
			if (string.IsNullOrWhiteSpace(notes))
				return;

			ctx.Call.AddAttachment(new AdditionalMessageContentPart
			{
				Content = $"Additional notes from user: {notes}",
				IsVisible = false,
				IsRestorable = false
			});
		}
	}
}
