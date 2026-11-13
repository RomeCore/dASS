using System.ComponentModel;

namespace LLMDesktopAssistant.LLM.Services
{
	public interface IChatExecutionStatusService : INotifyPropertyChanged
	{
		/// <summary>
		/// Gets a value indicating whether the chat is currently executing.
		/// This is observable and will raise a PropertyChanged event when its value changes.
		/// </summary>
		bool IsExecuting { get; }

		/// <summary>
		/// Brings the chat into an executing state, incrementing the inner counter.
		/// </summary>
		/// <returns>
		/// A disposable object that, when disposed,
		/// will decrement the inner counter and potentially exit the executing state.
		/// </returns>
		IDisposable WithExecution();

		/// <summary>
		/// Waits until the chat is idle (<see cref="IsExecuting"/> != true).
		/// </summary>
		/// <param name="cancellationToken">Token to cancel the operation.</param>
		/// <returns>A task that completes when the chat is idle.</returns>
		Task WaitUntilIdleAsync(CancellationToken cancellationToken = default);
	}
}