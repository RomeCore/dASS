using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.Tools.Consents;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Tools.Specifiers;

namespace LLMDesktopAssistant.Tools.Execution.Services
{
	/// <summary>
	/// The policy stage. Chat-free and stateless: the agent's auto-approve/disallow settings arrive on
	/// the context as <see cref="IToolExecutionContext.EffectiveBehaviours"/>.
	/// </summary>
	[Service(typeof(IToolPolicyService))]
	public sealed class ToolPolicyService(
		IToolApprovalService approvalService,
		IToolMemorizationService memorizationService) : IToolPolicyService
	{
		/// <inheritdoc/>
		public Task<ToolPolicyDecision> EvaluateAsync(IToolExecutionContext ctx, CancellationToken ct)
		{
			ct.ThrowIfCancellationRequested();

			var descriptor = ctx.Descriptor;
			var autoApprove = ctx.EffectiveBehaviours.AutoApproveBehaviours;
			var disallowed = ctx.EffectiveBehaviours.DisallowedBehaviours;
			var source = ToolDecisionSource.ApprovalLevel;

			if (descriptor.PolicyMask is { } mask)
			{
				var autoApproveBefore = autoApprove;
				var disallowedBefore = disallowed;

				autoApprove |= mask.AutoApproveBehaviours;
				disallowed |= mask.DisallowedBehaviours;
				autoApprove &= ~mask.DisallowedBehaviours;
				disallowed &= ~mask.AutoApproveBehaviours;

				if (autoApprove != autoApproveBefore || disallowed != disallowedBefore)
					source = ToolDecisionSource.PolicyMask;
			}

			var approvalLevel = descriptor.ApprovalLevel;
			var (decision, decisionMessage) = approvalService.ApproveTool(
				approvalLevel, ctx.Call.ExpectedBehaviour ?? descriptor.DefaultExpectedBehaviour, autoApprove, disallowed);

			if (approvalLevel.IsPolicyBased() &&
				descriptor.Specifier is { } specifierAnalyzer &&
				descriptor.Specifiers.Count > 0)
			{
				var specifier = SpecifierEngine.Evaluate(descriptor.Specifiers, specifierAnalyzer,
					ctx.Arguments.Effective, ctx, descriptor.SpecifierParameters,
					descriptor.SpecifierAggregationMode);

				var combined = SpecifierEngine.Combine(specifier.Verdict, decision,
					descriptor.SpecifierUnionMode ?? SpecifierBehaviourUnionMode.CombineSoft);

				if (combined != decision || specifier.Verdict == SpecifierVerdict.Deny)
					source = ToolDecisionSource.Specifier;

				decision = combined;

				if (decision == ToolPolicyDecision.Disallow && specifier.Verdict == SpecifierVerdict.Deny)
					decisionMessage = specifier.Message ?? decisionMessage;
			}

			if (approvalLevel.IsPolicyBased() &&
				memorizationService.TryGetMemorizedDecision(ctx.Chat, ctx.Call.ToolName,
					out var memorized, out var memorizedMessage) &&
				decision != ToolPolicyDecision.Disallow)
			{
				decision = memorized;
				source = ToolDecisionSource.Memorization;

				if (memorized == ToolPolicyDecision.Disallow)
					decisionMessage = memorizedMessage ?? "The tool execution was denied by the user.";
			}

			ctx.PolicyDecision = decision;
			ctx.PolicyReason = new ToolPolicyReason(source,
				string.IsNullOrWhiteSpace(decisionMessage) ? null : decisionMessage);

			return Task.FromResult(decision);
		}
	}
}
