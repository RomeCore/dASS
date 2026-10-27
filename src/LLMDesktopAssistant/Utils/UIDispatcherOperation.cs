using Avalonia.Threading;

namespace LLMDesktopAssistant.Utils
{
	/// <summary>
	/// A handle to work queued by <see cref="UIDispatcher"/>. When the work ran inline there is nothing
	/// left to abort, so the handle is a no-op instead of a <see cref="DispatcherOperation"/>.
	/// </summary>
	public sealed class UIDispatcherOperation
	{
		private readonly DispatcherOperation? _operation;

		/// <summary>
		/// Initializes a new instance of the <see cref="UIDispatcherOperation"/> class.
		/// </summary>
		/// <param name="operation">The queued Avalonia operation, or <see langword="null"/> when the work already ran inline.</param>
		internal UIDispatcherOperation(DispatcherOperation? operation)
		{
			_operation = operation;
		}

		/// <summary>
		/// Gets a handle whose work already ran and therefore cannot be aborted.
		/// </summary>
		public static UIDispatcherOperation Completed { get; } = new(null);

		/// <summary>
		/// Cancels the queued work unless it already ran.
		/// </summary>
		public void Abort() => _operation?.Abort();
	}
}
