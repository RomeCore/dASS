namespace LLMDesktopAssistant.LLM.Services
{
	/// <summary>
	/// Manages the status of chat execution flow.
	/// Counts only the main flow (the <see cref="IChatExecutionService"/>),
	/// while <see cref="IExecutionInfoStatusService"/> couns all (including agentic tasks and tools).
	/// </summary>
	[ChatService(typeof(IChatExecutionStatusService))]
	public class ChatExecutionStatusService : NotifyPropertyChanged, IChatExecutionStatusService
	{
		private readonly Lock _lock = new();
		private int _executionCount;
		private TaskCompletionSource? _executionCompletionSource;

		/// <inheritdoc/>
		public bool IsExecuting
		{
			get;
			private set => SetProperty(ref field, value);
		}

		/// <inheritdoc/>
		public IDisposable WithExecution()
		{
			lock (_lock)
			{
				_executionCount++;
				IsExecuting = _executionCount > 0;
			}
			return new Disposable(() =>
			{
				lock (_lock)
				{
					_executionCount--;
					var prevIsExecuting = IsExecuting;
					IsExecuting = _executionCount > 0;

					if (prevIsExecuting && !IsExecuting)
					{
						_executionCompletionSource?.SetResult();
						_executionCompletionSource = null;
					}
				}
			});
		}

		/// <inheritdoc/>
		public Task WaitUntilIdleAsync(CancellationToken cancellationToken = default)
		{
			lock (_lock)
			{
				if (!IsExecuting)
					return cancellationToken.IsCancellationRequested
						? Task.FromCanceled(cancellationToken)
						: Task.CompletedTask;
				return (_executionCompletionSource ?? new()).Task.WaitAsync(cancellationToken);
			}
		}
	}
}
