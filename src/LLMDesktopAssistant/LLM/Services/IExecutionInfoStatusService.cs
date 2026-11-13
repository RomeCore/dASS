using System.ComponentModel;
using LLMDesktopAssistant.LLM.Domain;

namespace LLMDesktopAssistant.LLM.Services
{
	/// <summary>
	/// Tracks the informational execution status of a chat session and provides scoped markers
	/// for executions and user confirmations.
	/// </summary>
	public interface IExecutionInfoStatusService : INotifyPropertyChanged
	{
		/// <summary>
		/// Gets the current execution status of the chat session.
		/// </summary>
		ChatStatus Status { get; }

		/// <summary>
		/// Marks the chat as executing. The returned <see cref="IDisposable"/> must be
		/// disposed when the execution finishes; the status is restored automatically.
		/// </summary>
		/// <returns>A disposable that ends the execution marker.</returns>
		IDisposable WithExecution();

		/// <summary>
		/// Marks the chat as waiting for a user confirmation. The returned
		/// <see cref="IDisposable"/> must be disposed when the confirmation is resolved;
		/// the status is restored automatically.
		/// </summary>
		/// <returns>A disposable that ends the confirmation marker.</returns>
		IDisposable WithConfirmation();
	}
}
