namespace LLMDesktopAssistant.Tools.Execution.Domain
{
	/// <summary>
	/// A tool invocation failure whose message is model-facing: the settlement stage writes it into the
	/// call verbatim, without adding a generic prefix.
	/// </summary>
	/// <remarks>
	/// The pipeline uses one error channel. A stage that can refuse an invocation throws this exception;
	/// the orchestrator catches everything and hands it to the settlement stage, which is the single owner
	/// of the call's terminal state.
	/// </remarks>
	public class ToolInvocationException : Exception
	{
		/// <summary>
		/// Creates the exception.
		/// </summary>
		/// <param name="message">The model-facing explanation.</param>
		public ToolInvocationException(string message) : base(message)
		{
		}

		/// <summary>
		/// Creates the exception.
		/// </summary>
		/// <param name="message">The model-facing explanation.</param>
		/// <param name="innerException">The cause.</param>
		public ToolInvocationException(string message, Exception innerException) : base(message, innerException)
		{
		}
	}
}
