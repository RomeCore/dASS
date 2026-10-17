using LLMDesktopAssistant.Tests.Tools;
using LLMDesktopAssistant.Tools.Execution.Contexts;
using LLMDesktopAssistant.Tools.Execution.Dispatch;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Tools.Execution.Orchestrators;
using LLMDesktopAssistant.Tools.Execution.Sinks;

namespace LLMDesktopAssistant.Tests.Tools.Execution
{
	/// <summary>
	/// The dispatcher: it builds the context through the request's factory, substitutes the missing-tool
	/// descriptor and routes every source to its own orchestrator.
	/// </summary>
	public class ToolInvocationDispatcherTests
	{
		[Fact]
		public async Task RunAsync_SubstitutesTheMissingToolDescriptor()
		{
			var stages = new RecordingStages();
			var session = ToolTestContext.Create(ToolTestContext.CreateTool());
			var factory = new CapturingFactory(session.Context);

			await CreateDispatcher(stages).RunAsync(
				CreateRequest(ToolInvocationSource.ChatRun, session.Call, factory, descriptor: null),
				CancellationToken.None);

			Assert.Same(MissingToolDescriptor.Instance, factory.Parameters!.Descriptor);
			Assert.Equal(session.Context.Call, factory.Parameters.Call);
			Assert.Contains("validation", stages.Calls);
			Assert.Contains("settlement", stages.Calls);
		}

		[Fact]
		public async Task RunAsync_PassesTheDescriptorThroughWhenItExists()
		{
			var stages = new RecordingStages();
			var tool = ToolTestContext.CreateTool();
			var session = ToolTestContext.Create(tool);
			var factory = new CapturingFactory(session.Context);

			await CreateDispatcher(stages).RunAsync(
				CreateRequest(ToolInvocationSource.ChatRun, session.Call, factory, tool),
				CancellationToken.None);

			Assert.Same(tool, factory.Parameters!.Descriptor);
		}

		[Fact]
		public async Task RunAsync_RoutesAgentTaskToTheTaskPipeline()
		{
			var stages = new RecordingStages();
			var session = ToolTestContext.Create(ToolTestContext.CreateTool());
			var factory = new CapturingFactory(session.Context);

			await CreateDispatcher(stages).RunAsync(
				CreateRequest(ToolInvocationSource.AgentTask, session.Call,
					factory, ToolTestContext.CreateTool()),
				CancellationToken.None);

			Assert.Contains("policy", stages.Calls);
			Assert.Contains("confirmation", stages.Calls);
			Assert.DoesNotContain("streaming", stages.Calls);
		}

		[Fact]
		public async Task RunAsync_RoutesScriptToTheScriptPipeline()
		{
			var stages = new RecordingStages();
			var session = ToolTestContext.Create(ToolTestContext.CreateTool());
			var factory = new CapturingFactory(session.Context);

			await CreateDispatcher(stages).RunAsync(
				CreateRequest(ToolInvocationSource.Script, session.Call, factory,
					ToolTestContext.CreateTool()),
				CancellationToken.None);

			Assert.Equal(["validation", "preview", "execution", "settlement"], stages.Calls);
		}

		[Fact]
		public async Task RunAsync_UnknownSourceThrows()
		{
			var stages = new RecordingStages();
			var session = ToolTestContext.Create(ToolTestContext.CreateTool());
			var factory = new CapturingFactory(session.Context);

			await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => CreateDispatcher(stages).RunAsync(
				CreateRequest((ToolInvocationSource)999, session.Call, factory,
					ToolTestContext.CreateTool()),
				CancellationToken.None));
		}

		private static ToolInvocationDispatcher CreateDispatcher(RecordingStages stages) => new(
			new ChatToolOrchestrator(stages, stages, stages, stages, stages, stages, stages, stages),
			new ChatRestartOrchestrator(stages, stages, stages, stages, stages, stages),
			new ToolCommandOrchestrator(stages, stages, stages, stages, stages, stages),
			new AgentTaskToolOrchestrator(stages, stages, stages, stages, stages, stages, stages),
			new ScriptToolOrchestrator(stages, stages, stages, stages));

		private static ToolInvocationRequest CreateRequest(ToolInvocationSource source, IToolCall call,
			IToolExecutionContextFactory factory, IToolDescriptor? descriptor) => new()
		{
			Source = source,
			Call = call,
			Descriptor = descriptor,
			Confirmation = ConstantConfirmationSink.Approve,
			ContextFactory = factory
		};

		private sealed class CapturingFactory(IToolExecutionContext context) : IToolExecutionContextFactory
		{
			public ToolContextCreationParams? Parameters { get; private set; }

			public IToolExecutionContext CreateContext(in ToolContextCreationParams parameters)
			{
				Parameters = parameters;
				return context;
			}
		}
	}
}
