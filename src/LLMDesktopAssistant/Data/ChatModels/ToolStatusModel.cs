namespace LLMDesktopAssistant.Data.ChatModels
{
	/// <summary>
	/// The persisted outcome of a tool call. This is the storage vocabulary, not the live one: the pipeline
	/// works with <see cref="LLM.Domain.ToolStatus"/>, which has more members, and the synchronizer maps
	/// between the two.
	/// </summary>
	public enum ToolStatusModel
	{
		/// <summary>
		/// The call never started, or was still running when the chat was closed.
		/// </summary>
		NotExecuted,

		/// <summary>
		/// The call had already entered execution when the chat was closed, so its outcome is unknown.
		/// </summary>
		ExecutionBegin,

		/// <summary>
		/// The call completed successfully.
		/// </summary>
		Success,

		/// <summary>
		/// The call was cancelled, by the user or by the run being torn down.
		/// </summary>
		Cancelled,

		/// <summary>
		/// The call failed.
		/// </summary>
		Error,

		/// <summary>
		/// The call was refused before it ran - by a policy decision or by a hook - and the model received the
		/// reason instead of a result.
		/// </summary>
		/// <remarks>
		/// Appended last on purpose: the values are persisted as integers, so a new member must never shift
		/// the ones above it.
		/// </remarks>
		Rejected
	}
}
