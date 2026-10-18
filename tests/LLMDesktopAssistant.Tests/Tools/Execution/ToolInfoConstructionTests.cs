using LLMDesktopAssistant.Tools;
using LLMDesktopAssistant.Tools.Execution.Domain;

namespace LLMDesktopAssistant.Tests.Tools.Execution
{
	/// <summary>
	/// The construction-time half of the handled-decision contract: a tool that claims a decision it cannot
	/// surface is rejected before it can reach the pipeline.
	/// </summary>
	public class ToolInfoConstructionTests
	{
		[Fact]
		public void HandledDecisionsWithoutProvider_Throws()
		{
			var info = new ToolInitializationInfo
			{
				Name = "broken-handler",
				Description = "Declares a handled decision without a provider.",
				Executor = (Action)(() => { }),
				HandledDecisions = ToolPolicyDecision.Ask
			};

			Assert.Throws<InvalidOperationException>(() => ToolInfo.Create(info));
		}

		[Fact]
		public void HandledAllWithoutProvider_Throws()
		{
			var info = new ToolInitializationInfo
			{
				Name = "broken-all-handler",
				Description = "Declares every handled decision without a provider.",
				Executor = (Action)(() => { }),
				HandledDecisions = ToolPolicyDecision.All
			};

			Assert.Throws<InvalidOperationException>(() => ToolInfo.Create(info));
		}

		[Fact]
		public void HandledApproveWithoutProvider_IsAllowed()
		{
			var info = new ToolInitializationInfo
			{
				Name = "inert-approve",
				Description = "Approve is not a confirmation decision.",
				Executor = (Action)(() => { }),
				HandledDecisions = ToolPolicyDecision.Approve
			};

			var tool = ToolInfo.Create(info);

			Assert.Equal(ToolPolicyDecision.Approve, tool.HandledDecisions);
			Assert.Null(tool.Approval);
		}

		[Fact]
		public void HandledAskWithProvider_IsAllowed()
		{
			var info = new ToolInitializationInfo
			{
				Name = "self-confirming",
				Description = "A tool with its own approval surface.",
				Executor = (Action)(() => { }),
				HandledDecisions = ToolPolicyDecision.Ask,
				Approval = (Func<IToolExecutionContext, CancellationToken, Task<ToolApprovalResult>>)
					((_, _) => Task.FromResult(ToolApprovalResult.ApprovedOnce()))
			};

			var tool = ToolInfo.Create(info);

			Assert.Equal(ToolPolicyDecision.Ask, tool.HandledDecisions);
			Assert.NotNull(tool.Approval);
		}
	}
}
