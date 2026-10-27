using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.Tests.Utils
{
	public class UIDispatcherTests
	{
		[Fact]
		public void InlineMode_ReportsItselfAsTheUIThread()
		{
			Assert.False(UIDispatcher.Delegating);
			Assert.True(UIDispatcher.IsUIThread);
		}

		[Fact]
		public void Invoke_RunsTheActionOnTheCallingThread()
		{
			var callingThread = Environment.CurrentManagedThreadId;
			int actionThread = -1;

			UIDispatcher.Invoke(() => actionThread = Environment.CurrentManagedThreadId);

			Assert.Equal(callingThread, actionThread);
		}

		[Fact]
		public void Post_RunsTheActionBeforeReturning()
		{
			bool ran = false;

			UIDispatcher.Post(() => ran = true);

			Assert.True(ran);
		}

		[Fact]
		public void Post_WithAsyncAction_StartsItSynchronously()
		{
			bool started = false;

			UIDispatcher.Post(async () =>
			{
				started = true;
				await Task.Yield();
			});

			Assert.True(started);
		}

		[Fact]
		public void InvokeAsync_RunsTheActionAndReturnsACompletedHandle()
		{
			bool ran = false;

			var operation = UIDispatcher.InvokeAsync(() => ran = true);

			Assert.True(ran);
			Assert.Same(UIDispatcherOperation.Completed, operation);
		}

		[Fact]
		public void InvokeAsync_AbortAfterExecution_KeepsTheResult()
		{
			bool ran = false;
			var operation = UIDispatcher.InvokeAsync(() => ran = true);

			operation.Abort();

			Assert.True(ran);
		}
	}
}
