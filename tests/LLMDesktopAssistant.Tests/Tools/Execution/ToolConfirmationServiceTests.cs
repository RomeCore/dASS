using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.MVVM.Additional;
using LLMDesktopAssistant.Tests.Tools;
using LLMDesktopAssistant.Tools;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Tools.Execution.Services;

namespace LLMDesktopAssistant.Tests.Tools.Execution
{
	/// <summary>
	/// The confirmation stage: an unhandled decision is served by the invocation's surface, a handled one
	/// is left to the tool, and the user's notes land on the call as an invisible content part.
	/// </summary>
	public class ToolConfirmationServiceTests
	{
		[Fact]
		public async Task AnUnhandledDisallow_WritesTheReasonAndStops()
		{
			var session = ToolTestContext.Create(ToolTestContext.CreateTool());
			session.Context.PolicyDecision = ToolPolicyDecision.Disallow;
			session.Context.PolicyReason = new ToolPolicyReason(ToolDecisionSource.ApprovalLevel, "policy says no");

			var approved = await new ToolConfirmationService().ConfirmAsync(session.Context, CancellationToken.None);

			Assert.False(approved);
			Assert.Equal(ToolStatus.Error, session.Call.Status);
			Assert.Equal("policy says no", session.Call.ResultContent);
		}

		[Fact]
		public async Task AnUnhandledAsk_UsesTheSinkAndWritesTheCancellation()
		{
			var sink = new FixedConfirmationSink(ToolApprovalResult.DeniedOnce("too risky"));
			var session = ToolTestContext.Create(ToolTestContext.CreateTool(), confirmation: sink);
			session.Context.PolicyDecision = ToolPolicyDecision.Ask;

			var approved = await new ToolConfirmationService().ConfirmAsync(session.Context, CancellationToken.None);

			Assert.False(approved);
			Assert.Equal(1, sink.Calls);
			Assert.Equal(ToolStatus.Cancelled, session.Call.Status);
			Assert.Contains("too risky", session.Call.ResultContent ?? string.Empty);
		}

		[Fact]
		public async Task AnApprovedAsk_AddsTheNotesAsAnInvisibleContentPart()
		{
			var sink = new FixedConfirmationSink(ToolApprovalResult.ApprovedOnce("looks fine"));
			var session = ToolTestContext.Create(ToolTestContext.CreateTool(), confirmation: sink);
			session.Context.PolicyDecision = ToolPolicyDecision.Ask;

			var approved = await new ToolConfirmationService().ConfirmAsync(session.Context, CancellationToken.None);

			Assert.True(approved);
			var part = Assert.Single(session.Call.AdditionalData.OfType<AdditionalMessageContentPart>());
			Assert.Equal("Additional notes from user: looks fine", part.Content);
			Assert.False(part.IsVisible);
			Assert.False(part.IsRestorable);
		}

		[Fact]
		public async Task AHandledAsk_IsNotAskedAgain()
		{
			var sink = new FixedConfirmationSink(ToolApprovalResult.DeniedOnce());
			var tool = ToolTestContext.CreateTool("self-confirming",
				info => info.HandledDecisions = ToolPolicyDecision.Ask);
			var session = ToolTestContext.Create(tool, confirmation: sink);
			session.Context.PolicyDecision = ToolPolicyDecision.Ask;

			var approved = await new ToolConfirmationService().ConfirmAsync(session.Context, CancellationToken.None);

			Assert.True(approved);
			Assert.Equal(0, sink.Calls);
		}

		private sealed class FixedConfirmationSink(ToolApprovalResult result) : IConfirmationSink
		{
			public int Calls { get; private set; }

			public Task<ToolApprovalResult> RequestAsync(IToolExecutionContext ctx, CancellationToken ct)
			{
				Calls++;
				return Task.FromResult(result);
			}
		}
	}
}
