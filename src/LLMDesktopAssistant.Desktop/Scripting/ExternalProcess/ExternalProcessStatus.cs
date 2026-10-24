namespace LLMDesktopAssistant.Desktop.Scripting.ExternalProcess
{
	/// <summary>
	/// How one script invocation ended.
	/// </summary>
	public enum ExternalProcessStatus
	{
		Succeeded = 0,

		/// <summary>
		/// The process exited with a non-zero code.
		/// </summary>
		ProcessFailed,

		/// <summary>
		/// The process succeeded but the promised structure is missing or unparsable.
		/// </summary>
		InvalidOutput,

		/// <summary>
		/// The process was killed by its timeout.
		/// </summary>
		TimedOut,

		/// <summary>
		/// The caller cancelled the invocation.
		/// </summary>
		Cancelled,
	}
}
