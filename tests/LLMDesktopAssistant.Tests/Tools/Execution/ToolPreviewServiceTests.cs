using LLMDesktopAssistant.Tests.Tools;
using LLMDesktopAssistant.Tools;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Tools.Execution.Services;

namespace LLMDesktopAssistant.Tests.Tools.Execution
{
	/// <summary>
	/// The preview stage's handled-decision override: preview may narrow or widen the descriptor mask for
	/// one invocation, and the context carries the result into the confirmation stage.
	/// </summary>
	public class ToolPreviewServiceTests
	{
		[Fact]
		public async Task ThePreviewOverride_ReplacesTheDescriptorMask()
		{
			var tool = ToolTestContext.CreateTool("preview-override",
				previewExecutor: (Func<IToolExecutionContext, PreviewToolExecutionResult>)
					(_ => new PreviewToolExecutionResult { HandledDecisions = ToolPolicyDecision.Ask }));
			var session = ToolTestContext.Create(tool);
			Assert.Equal(ToolPolicyDecision.None, session.Context.HandledDecisions);

			var continued = await new ToolPreviewService().RunAsync(session.Context, CancellationToken.None);

			Assert.True(continued);
			Assert.Equal(ToolPolicyDecision.Ask, session.Context.HandledDecisions);
		}

		[Fact]
		public async Task ThePreviewWithoutOverride_KeepsTheDescriptorMask()
		{
			var tool = ToolTestContext.CreateTool("preview-no-override",
				info => info.HandledDecisions = ToolPolicyDecision.Approve);
			var session = ToolTestContext.Create(tool);

			var continued = await new ToolPreviewService().RunAsync(session.Context, CancellationToken.None);

			Assert.True(continued);
			Assert.Equal(ToolPolicyDecision.Approve, session.Context.HandledDecisions);
		}
	}
}
