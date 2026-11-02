using System.ComponentModel;
using LLMDesktopAssistant.LLM.Domain;

namespace LLMDesktopAssistant.LLM.Services
{
	/// <summary>
	/// The default implementation of <see cref="IChatExecutionStatusService"/>.
	/// Maintains counters of active executions and confirmations and updates the
	/// <see cref="Status"/> with the priority: Confirming &gt; Executing &gt; Idle.
	/// </summary>
	[ChatService(typeof(IChatExecutionStatusService))]
	public class ChatExecutionStatusService : NotifyPropertyChanged, IChatExecutionStatusService
	{
		private int _executionCount;
		private int _confirmationCount;

		private ChatStatus _status;
		/// <inheritdoc/>
		public ChatStatus Status
		{
			get => _status;
			private set => SetProperty(ref _status, value);
		}

		/// <inheritdoc/>
		public IDisposable WithExecution()
		{
			Interlocked.Increment(ref _executionCount);
			UpdateStatus();
			return new Disposable(() =>
			{
				Interlocked.Decrement(ref _executionCount);
				UpdateStatus();
			});
		}

		/// <inheritdoc/>
		public IDisposable WithConfirmation()
		{
			Interlocked.Increment(ref _confirmationCount);
			UpdateStatus();
			return new Disposable(() =>
			{
				Interlocked.Decrement(ref _confirmationCount);
				UpdateStatus();
			});
		}

		/// <inheritdoc/>
		public async Task WaitUntilIdleAsync(CancellationToken ct = default)
		{
			while (Status != ChatStatus.Idle)
			{
				var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

				void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
				{
					if (e.PropertyName == nameof(Status) && Status == ChatStatus.Idle)
						tcs.TrySetResult();
				}

				PropertyChanged += OnPropertyChanged;
				try
				{
					// Re-check inside the subscription: the transition may have happened before it was attached.
					if (Status == ChatStatus.Idle)
						return;

					using var registration = ct.Register(() => tcs.TrySetCanceled(ct));
					await tcs.Task;
				}
				finally
				{
					PropertyChanged -= OnPropertyChanged;
				}
			}
		}

		private void UpdateStatus()
		{
			var status = _confirmationCount > 0 ? ChatStatus.Confirming
				: _executionCount > 0 ? ChatStatus.Executing
				: ChatStatus.Idle;
			Status = status;
		}
	}
}
