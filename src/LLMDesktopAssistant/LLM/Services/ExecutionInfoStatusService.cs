using LLMDesktopAssistant.LLM.Domain;

namespace LLMDesktopAssistant.LLM.Services
{
	/// <summary>
	/// The default implementation of <see cref="IExecutionInfoStatusService"/>.
	/// Maintains counters of active executions and confirmations and updates the
	/// <see cref="Status"/> with the priority: Confirming &gt; Executing &gt; Idle.
	/// </summary>
	[ChatService(typeof(IExecutionInfoStatusService))]
	public class ExecutionInfoStatusService : NotifyPropertyChanged, IExecutionInfoStatusService
	{
		private int _executionCount;
		private int _confirmationCount;

		/// <inheritdoc/>
		public ChatStatus Status
		{
			get;
			private set => SetProperty(ref field, value);
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

		private void UpdateStatus()
		{
			var status = _confirmationCount > 0 ? ChatStatus.Confirming
				: _executionCount > 0 ? ChatStatus.Executing
				: ChatStatus.Idle;
			Status = status;
		}
	}
}
