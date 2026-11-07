namespace LLMDesktopAssistant.LLM.Domain
{
	/// <summary>
	/// Represents the status of a tool.
	/// </summary>
	public enum ToolStatus
	{
		/// <summary>
		/// The tool is just created and not yet initialized.
		/// </summary>
		None,

		/// <summary>
		/// The tool call is currently pending and waiting to complete arguments generation from LLM.
		/// </summary>
		Pending,

		/// <summary>
		/// The tool is currently waiting inside synchronization queue.
		/// </summary>
		Queued,

		/// <summary>
		/// The tool is pre-executing.
		/// </summary>
		PreExecuting,

		/// <summary>
		/// The tool is currently waiting for user's approval before proceeding.
		/// </summary>
		WaitingForApproval,

		/// <summary>
		/// The tool execution has been interrupted. This happens when user exits from application and loads the chat history again.
		/// </summary>
		ExecutionInterrupted,

		/// <summary>
		/// The tool is currently executing.
		/// </summary>
		Executing,

		/// <summary>
		/// The tool has completed successfully.
		/// </summary>
		Success,

		/// <summary>
		/// The tool has failed to complete.
		/// </summary>
		Error,

		/// <summary>
		/// The call was refused before it ran - by a policy decision or by a hook - and the model received the
		/// reason instead of a result. Unlike <see cref="Error"/> this is not a malfunction.
		/// </summary>
		Rejected,

		/// <summary>
		/// The tool was cancelled by the user.
		/// </summary>
		Cancelled
	}

	/// <summary>
	/// Helpers for <see cref="ToolStatus"/>.
	/// </summary>
	public static class ToolStatusExtensions
	{
		/// <summary>
		/// Determines whether the status is terminal: the call will not change any more and its
		/// completion state is final.
		/// </summary>
		/// <param name="status">The status to check.</param>
		/// <returns><see langword="true"/> for <see cref="ToolStatus.Success"/>,
		/// <see cref="ToolStatus.Error"/>, <see cref="ToolStatus.Rejected"/>, <see cref="ToolStatus.Cancelled"/>
		/// and <see cref="ToolStatus.ExecutionInterrupted"/>.</returns>
		public static bool IsTerminal(this ToolStatus status) =>
			status is ToolStatus.Success or ToolStatus.Error or ToolStatus.Rejected
				or ToolStatus.Cancelled or ToolStatus.ExecutionInterrupted;
	}
}