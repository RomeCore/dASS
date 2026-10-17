using LLMDesktopAssistant.Tools;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Tools.Execution.Services;
using LLMDesktopAssistant.Tests.Tools;

namespace LLMDesktopAssistant.Tests.Tools.Execution
{
	/// <summary>
	/// The keyed scope: atomic acquisition of the whole key set, hierarchical conflicts, canonicalization
	/// and cancellation while waiting.
	/// </summary>
	public class ToolSynchronizationServiceTests
	{
		private static readonly string Root = Path.Combine(Path.GetTempPath(), "dass-sync-tests");

		[Fact]
		public async Task AnInvocationWithoutKeys_RunsTheInnerSteps()
		{
			var service = new ToolSynchronizationService();
			var session = ToolTestContext.Create(ToolTestContext.CreateTool());
			var ran = false;

			await service.RunAsync(session.Context, () =>
			{
				ran = true;
				return Task.CompletedTask;
			}, CancellationToken.None);

			Assert.True(ran);
		}

		[Fact]
		public async Task InvocationsWithTheSameFileKey_AreSerialized() =>
			Assert.Equal(1, await MeasureAsync(FileContext("same.txt"), FileContext("same.txt")));

		[Fact]
		public async Task InvocationsWithDifferentFileKeys_RunConcurrently() =>
			Assert.Equal(2, await MeasureAsync(FileContext("a.txt"), FileContext("b.txt"), overlap: true));

		[Fact]
		public async Task AFileBeneathAHeldDirectory_Conflicts() =>
			Assert.Equal(1, await MeasureAsync(
				Context(Path.Combine(Root, "dir"), ToolSyncKeyKind.Directory),
				Context(Path.Combine(Root, "dir", "a.txt"), ToolSyncKeyKind.File)));

		[Fact]
		public async Task AnAllFilesKey_ConflictsWithEveryFileKey() =>
			Assert.Equal(1, await MeasureAsync(
				Context(string.Empty, ToolSyncKeyKind.AllFiles),
				FileContext("a.txt")));

		[Fact]
		public async Task AnAllFilesKey_DoesNotConflictWithACustomKey() =>
			Assert.Equal(2, await MeasureAsync(
				Context(string.Empty, ToolSyncKeyKind.AllFiles),
				Context("wd-switch", ToolSyncKeyKind.Custom),
				overlap: true));

		[Fact]
		public async Task OverlappingKeySetsDoNotDeadlock()
		{
			var service = new ToolSynchronizationService();

			var first = RunAsync(service, [Path.Combine(Root, "a.txt"), Path.Combine(Root, "b.txt")]);
			var second = RunAsync(service, [Path.Combine(Root, "b.txt"), Path.Combine(Root, "c.txt")]);

			await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5));
		}

		[Fact]
		public async Task CancellationWhileWaiting_UnblocksWithoutEntering()
		{
			var service = new ToolSynchronizationService();
			var session = FileContext("cancelled.txt");

			var held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
			var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

			var holder = service.RunAsync(session.Context, async () =>
			{
				entered.TrySetResult();
				await held.Task;
			}, CancellationToken.None);

			await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

			using var cts = new CancellationTokenSource();
			var waiterEntered = false;
			var waiter = service.RunAsync(session.Context, () =>
			{
				waiterEntered = true;
				return Task.CompletedTask;
			}, cts.Token);

			await Task.Delay(50);
			cts.Cancel();

			await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiter);
			Assert.False(waiterEntered);

			held.SetResult();
			await holder;
		}

		[Fact]
		public void Canonicalize_NormalizesDeduplicatesAndCollapses()
		{
			var directory = Path.Combine(Root, "canon");
			var file = Path.Combine(directory, "a.txt");

			var keys = ToolSynchronizationService.Canonicalize(
			[
				new ToolSyncKey(ToolSyncKeyKind.Directory, directory + Path.DirectorySeparatorChar),
				new ToolSyncKey(ToolSyncKeyKind.Directory, directory),
				new ToolSyncKey(ToolSyncKeyKind.File, file),
				new ToolSyncKey(ToolSyncKeyKind.File, string.Empty),
				new ToolSyncKey(ToolSyncKeyKind.AllFiles, "ignored")
			]);

			Assert.Single(keys, key => key.Kind == ToolSyncKeyKind.Directory);
			Assert.DoesNotContain(keys, key => key.Kind == ToolSyncKeyKind.File);
			Assert.Equal(string.Empty, Assert.Single(keys, key => key.Kind == ToolSyncKeyKind.AllFiles).Value);
		}

		private static ToolTestContext.Session FileContext(string name) =>
			Context(Path.Combine(Root, name), ToolSyncKeyKind.File);

		private static ToolTestContext.Session Context(string value, ToolSyncKeyKind kind) =>
			ToolTestContext.Create(ToolTestContext.CreateTool("sync-tool",
				info => info.StaticSyncKeys = [new ToolSyncKey(kind, value)]));

		private static async Task RunAsync(ToolSynchronizationService service, string[] paths)
		{
			var session = ToolTestContext.Create(ToolTestContext.CreateTool("multi-key-tool",
				info => info.StaticSyncKeys = [.. paths.Select(path => new ToolSyncKey(ToolSyncKeyKind.File, path))]));

			await service.RunAsync(session.Context, () => Task.Delay(10), CancellationToken.None);
		}

		/// <summary>
		/// Runs two invocations and reports the maximum number of inner steps that ran at once.
		/// With <paramref name="overlap"/>, both inner steps must be inside the scope at the same time;
		/// a scope that wrongly serializes them times out instead of passing silently.
		/// </summary>
		private static async Task<int> MeasureAsync(ToolTestContext.Session first, ToolTestContext.Session second,
			bool overlap = false)
		{
			var service = new ToolSynchronizationService();
			var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
			var entered = 0;
			var lockObject = new object();
			var concurrent = 0;
			var maxConcurrent = 0;

			async Task RunAsync(ToolTestContext.Session session)
			{
				await service.RunAsync(session.Context, async () =>
				{
					lock (lockObject)
					{
						concurrent++;
						maxConcurrent = Math.Max(maxConcurrent, concurrent);
					}

					if (overlap)
					{
						if (Interlocked.Increment(ref entered) == 2)
							gate.TrySetResult();

						await gate.Task.WaitAsync(TimeSpan.FromSeconds(5));
					}
					else
					{
						await Task.Delay(50);
					}

					lock (lockObject)
						concurrent--;
				}, CancellationToken.None);
			}

			await Task.WhenAll(RunAsync(first), RunAsync(second)).WaitAsync(TimeSpan.FromSeconds(10));
			return maxConcurrent;
		}
	}
}
