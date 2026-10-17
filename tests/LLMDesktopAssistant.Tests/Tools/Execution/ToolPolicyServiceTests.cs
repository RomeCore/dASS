using LLMDesktopAssistant.Tests.Tools;
using LLMDesktopAssistant.Tools;
using LLMDesktopAssistant.Tools.Consents;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Tools.Execution.Services;

namespace LLMDesktopAssistant.Tests.Tools.Execution
{
	/// <summary>
	/// The policy stage: the approval level, the per-tool mask, the memorized decision and the reason
	/// they leave on the context.
	/// </summary>
	public class ToolPolicyServiceTests
	{
		[Fact]
		public async Task AlwaysDisallow_ProducesAReason()
		{
			var tool = ToolTestContext.CreateTool("disallowed",
				info => info.ApprovalLevel = ToolApprovalLevel.AlwaysDisallow);
			var session = ToolTestContext.Create(tool);

			var decision = await Service().EvaluateAsync(session.Context, CancellationToken.None);

			Assert.Equal(ToolPolicyDecision.Disallow, decision);
			Assert.Equal(ToolPolicyDecision.Disallow, session.Context.PolicyDecision);
			Assert.Equal(ToolDecisionSource.ApprovalLevel, session.Context.PolicyReason?.Source);
			Assert.False(string.IsNullOrWhiteSpace(session.Context.PolicyReason?.Message));
		}

		[Fact]
		public async Task AlwaysApprove_ApprovesWithoutAReason()
		{
			var tool = ToolTestContext.CreateTool("approved",
				info => info.ApprovalLevel = ToolApprovalLevel.AlwaysApprove);
			var session = ToolTestContext.Create(tool);

			var decision = await Service().EvaluateAsync(session.Context, CancellationToken.None);

			Assert.Equal(ToolPolicyDecision.Approve, decision);
			Assert.Null(session.Context.PolicyReason?.Message);
		}

		[Fact]
		public async Task ThePolicyMaskDisallowWinsOverTheAgentAutoApprove()
		{
			var tool = ToolTestContext.CreateTool("masked", info =>
			{
				info.ApprovalLevel = ToolApprovalLevel.PolicyBased;
				info.DefaultExpectedBehaviour = ToolBehaviour.FileEdit;
				info.PolicyMask = new ToolPolicyMask
				{
					AutoApproveBehaviours = ToolBehaviour.None,
					DisallowedBehaviours = ToolBehaviour.FileEdit
				};
			});

			var session = ToolTestContext.Create(tool,
				autoApprove: ToolBehaviour.FileEdit,
				disallowed: ToolBehaviour.None);
			session.Call.ExpectedBehaviour = ToolBehaviour.FileEdit;

			var decision = await Service().EvaluateAsync(session.Context, CancellationToken.None);

			Assert.Equal(ToolPolicyDecision.Disallow, decision);
			Assert.Equal(ToolDecisionSource.PolicyMask, session.Context.PolicyReason?.Source);
		}

		[Fact]
		public async Task AMemorizedDenial_OverridesAsk()
		{
			var tool = ToolTestContext.CreateTool("memorized", info =>
			{
				info.ApprovalLevel = ToolApprovalLevel.PolicyBased;
				info.DefaultExpectedBehaviour = ToolBehaviour.FileRead;
			});

			var memorization = new ToolMemorizationService();
			var session = ToolTestContext.Create(tool);
			session.Call.ExpectedBehaviour = ToolBehaviour.FileRead;

			memorization.MemorizeConsent(session.Context.Chat, tool.Name, new ToolConsentResult
			{
				IsApproved = false,
				Memorization = ToolApprovalMemorization.Session,
				Notes = "not today"
			});

			var decision = await new ToolPolicyService(new ToolApprovalService(), memorization)
				.EvaluateAsync(session.Context, CancellationToken.None);

			Assert.Equal(ToolPolicyDecision.Disallow, decision);
			Assert.Equal(ToolDecisionSource.Memorization, session.Context.PolicyReason?.Source);
			Assert.Contains("not today", session.Context.PolicyReason?.Message ?? string.Empty);
		}

		[Fact]
		public async Task AMemorizedApproval_OverridesAsk()
		{
			var tool = ToolTestContext.CreateTool("memorized-approve", info =>
			{
				info.ApprovalLevel = ToolApprovalLevel.PolicyBased;
				info.DefaultExpectedBehaviour = ToolBehaviour.FileRead;
			});

			var memorization = new ToolMemorizationService();
			var session = ToolTestContext.Create(tool);
			session.Call.ExpectedBehaviour = ToolBehaviour.FileRead;

			memorization.MemorizeConsent(session.Context.Chat, tool.Name, new ToolConsentResult
			{
				IsApproved = true,
				Memorization = ToolApprovalMemorization.Session,
				Notes = null
			});

			var decision = await new ToolPolicyService(new ToolApprovalService(), memorization)
				.EvaluateAsync(session.Context, CancellationToken.None);

			Assert.Equal(ToolPolicyDecision.Approve, decision);
			Assert.Equal(ToolDecisionSource.Memorization, session.Context.PolicyReason?.Source);
		}

		private static ToolPolicyService Service() =>
			new(new ToolApprovalService(), new ToolMemorizationService());
	}
}

