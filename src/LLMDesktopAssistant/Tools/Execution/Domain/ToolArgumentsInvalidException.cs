namespace LLMDesktopAssistant.Tools.Execution.Domain
{
	/// <summary>
	/// Thrown by the validation stage when the effective arguments do not satisfy the tool's argument
	/// schema. The message is shown to the model as the tool result.
	/// </summary>
	public sealed class ToolArgumentsInvalidException : ToolInvocationException
	{
		/// <summary>
		/// Creates the exception.
		/// </summary>
		/// <param name="message">The model-facing explanation.</param>
		public ToolArgumentsInvalidException(string message) : base(message)
		{
		}
	}
}
