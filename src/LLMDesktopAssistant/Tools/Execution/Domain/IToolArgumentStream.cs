namespace LLMDesktopAssistant.Tools.Execution.Domain
{
	/// <summary>
	/// The model's argument stream, neutralised for the execution core. The chat and the task worlds
	/// adapt their own partial tool call onto it; the core never sees a provider-specific type.
	/// </summary>
	public interface IToolArgumentStream
	{
		/// <summary>
		/// The arguments accumulated so far. May be partial JSON while the model is still emitting.
		/// </summary>
		string AccumulatedArgs { get; }

		/// <summary>
		/// Subscribes to argument updates. Dispose the returned subscription to detach; the streaming
		/// stage always detaches in a <c>finally</c>.
		/// </summary>
		/// <param name="handler">The callback invoked after each update.</param>
		/// <returns>A subscription that detaches the handler.</returns>
		IDisposable OnArgsUpdated(Action handler);

		/// <summary>
		/// Resolves when the model stopped emitting arguments. In <see cref="StreamingMode.Once"/> the
		/// stage awaits this before analysing the complete set.
		/// </summary>
		Task Drained { get; }
	}
}
