using LLMDesktopAssistant.Tests.Tools;
using LLMDesktopAssistant.Tools.Execution.Orchestrators;

namespace LLMDesktopAssistant.Tests.Tools.Execution
{
	/// <summary>
	/// The orchestrators: each source runs its own sequence, and the settlement stage receives the
	/// exception that interrupted it.
	/// </summary>
	public class ToolOrchestratorTests
	{
		[Fact]
		public async Task ChatRun_RunsTheFullPipelineInOrder()
		{
			var stages = new RecordingStages();
			var ctx = ToolTestContext.Create(ToolTestContext.CreateTool()).Context;
			var orchestrator = CreateChatRun(stages);

			await orchestrator.RunAsync(ctx, CancellationToken.None);

			Assert.Equal(
				["streaming", "validation", "synchronization", "preview", "policy", "confirmation", "execution",
					"settlement"],
				stages.Calls);
			Assert.True(stages.Settled);
			Assert.Null(stages.SettledException);
		}

		[Fact]
		public async Task ChatRun_StopsBeforeExecutionWhenConfirmationDenies()
		{
			var stages = new RecordingStages { Confirms = false };
			var ctx = ToolTestContext.Create(ToolTestContext.CreateTool()).Context;

			await CreateChatRun(stages).RunAsync(ctx, CancellationToken.None);

			Assert.DoesNotContain("execution", stages.Calls);
			Assert.True(stages.Settled);
			Assert.Null(stages.SettledException);
		}

		[Fact]
		public async Task ChatRun_SettlesWithTheExceptionThePreviewThrew()
		{
			var failure = new InvalidOperationException("preview broke");
			var stages = new RecordingStages { FailureStage = "preview", Failure = failure };
			var ctx = ToolTestContext.Create(ToolTestContext.CreateTool()).Context;

			await CreateChatRun(stages).RunAsync(ctx, CancellationToken.None);

			Assert.DoesNotContain("policy", stages.Calls);
			Assert.DoesNotContain("execution", stages.Calls);
			Assert.True(stages.Settled);
			Assert.Same(failure, stages.SettledException);
		}

		[Fact]
		public async Task ChatRestart_SkipsPolicyAndConfirmation()
		{
			var stages = new RecordingStages();
			var ctx = ToolTestContext.Create(ToolTestContext.CreateTool()).Context;

			await new ChatRestartOrchestrator(stages, stages, stages, stages, stages, stages)
				.RunAsync(ctx, CancellationToken.None);

			Assert.Equal(
				["streaming", "validation", "synchronization", "preview", "execution", "settlement"],
				stages.Calls);
		}

		[Fact]
		public async Task ToolCommand_RunsTheSameSequenceAsRestart()
		{
			var stages = new RecordingStages();
			var ctx = ToolTestContext.Create(ToolTestContext.CreateTool()).Context;

			await new ToolCommandOrchestrator(stages, stages, stages, stages, stages, stages)
				.RunAsync(ctx, CancellationToken.None);

			Assert.Equal(
				["streaming", "validation", "synchronization", "preview", "execution", "settlement"],
				stages.Calls);
		}

		[Fact]
		public async Task Script_SkipsStreamingSynchronizationPolicyAndConfirmation()
		{
			var stages = new RecordingStages();
			var ctx = ToolTestContext.Create(ToolTestContext.CreateTool()).Context;

			await new ScriptToolOrchestrator(stages, stages, stages, stages)
				.RunAsync(ctx, CancellationToken.None);

			Assert.Equal(["validation", "preview", "execution", "settlement"], stages.Calls);
		}

		[Fact]
		public async Task AgentTask_RunsTheFullPipelineWithoutStreaming()
		{
			var stages = new RecordingStages();
			var ctx = ToolTestContext.Create(ToolTestContext.CreateTool()).Context;

			await new AgentTaskToolOrchestrator(stages, stages, stages, stages, stages, stages, stages)
				.RunAsync(ctx, CancellationToken.None);

			Assert.Equal(
				["validation", "synchronization", "preview", "policy", "confirmation", "execution", "settlement"],
				stages.Calls);
		}

		private static ChatToolOrchestrator CreateChatRun(RecordingStages stages) =>
			new(stages, stages, stages, stages, stages, stages, stages, stages);
	}
}
