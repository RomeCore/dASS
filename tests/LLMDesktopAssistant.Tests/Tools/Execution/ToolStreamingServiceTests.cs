using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.Tests.Tools;
using LLMDesktopAssistant.Tools;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Tools.Execution.Services;

namespace LLMDesktopAssistant.Tests.Tools.Execution
{
	/// <summary>
	/// The streaming stage: it drains the neutral stream, keeps the call's arguments in sync and runs the
	/// analyzer in the mode the source selected.
	/// </summary>
	public class ToolStreamingServiceTests
	{
		[Fact]
		public async Task Once_DrainsAndWritesTheAccumulatedArguments()
		{
			var stream = new FakeArgumentStream();
			var session = ToolTestContext.Create(ToolTestContext.CreateTool(), argumentStream: stream);
			var service = new ToolStreamingService();

			var run = service.RunAsync(session.Context, StreamingMode.Once, CancellationToken.None);
			stream.Emit("""{"a":1}""");
			stream.Complete();

			await run.WaitAsync(TimeSpan.FromSeconds(5));

			Assert.Equal("""{"a":1}""", session.Call.Arguments);
			Assert.Equal(ToolStatus.Pending, session.Call.Status);
		}

		[Fact]
		public async Task Cyclic_UpdatesTheArgumentsOnEveryUpdate()
		{
			var stream = new FakeArgumentStream();
			var session = ToolTestContext.Create(ToolTestContext.CreateTool(), argumentStream: stream);
			var service = new ToolStreamingService();

			var run = service.RunAsync(session.Context, StreamingMode.Cyclic, CancellationToken.None);
			Assert.Equal(ToolStatus.Pending, session.Call.Status);

			stream.Emit("""{"a":""");
			Assert.Equal("""{"a":""", session.Call.Arguments);

			stream.Emit("""{"a":1}""");
			Assert.Equal("""{"a":1}""", session.Call.Arguments);

			stream.Complete();
			await run.WaitAsync(TimeSpan.FromSeconds(5));
		}

		[Fact]
		public async Task Once_WithoutAStream_StillRunsTheAnalyzer()
		{
			var analyzed = 0;
			var tool = ToolTestContext.CreateTool("once-analyzer",
				streamingAnalyzer: (Func<IToolExecutionContext, StreamingToolAnalysisResult>)(_ =>
				{
					analyzed++;
					return StreamingToolAnalysisResult.Continue;
				}));

			var session = ToolTestContext.Create(tool);

			await new ToolStreamingService().RunAsync(session.Context, StreamingMode.Once, CancellationToken.None);

			Assert.Equal(1, analyzed);
			Assert.Equal(ToolStatus.None, session.Call.Status);
		}

		[Fact]
		public async Task Cyclic_WithoutAStream_IsANoOp()
		{
			var session = ToolTestContext.Create(ToolTestContext.CreateTool());

			await new ToolStreamingService().RunAsync(session.Context, StreamingMode.Cyclic, CancellationToken.None);

			Assert.Equal(ToolStatus.None, session.Call.Status);
			Assert.Equal("{}", session.Call.Arguments);
		}

		private sealed class FakeArgumentStream : IToolArgumentStream
		{
			private readonly TaskCompletionSource _drained =
				new(TaskCreationOptions.RunContinuationsAsynchronously);

			private readonly List<Action> _handlers = [];

			public string AccumulatedArgs { get; private set; } = string.Empty;

			public Task Drained => _drained.Task;

			public IDisposable OnArgsUpdated(Action handler)
			{
				_handlers.Add(handler);
				return new Disposable(() => _handlers.Remove(handler));
			}

			public void Emit(string args)
			{
				AccumulatedArgs = args;
				foreach (var handler in _handlers.ToArray())
					handler();
			}

			public void Complete() => _drained.TrySetResult();
		}
	}
}
