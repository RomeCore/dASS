namespace LLMDesktopAssistant.Desktop.ToolModules.Terminal
{
	/// <summary>
	/// The lifecycle of the process shown by a <see cref="TerminalAdditionalViewModel"/>.
	/// </summary>
	public enum TerminalState
	{
		/// <summary>
		/// The process runs and the tool call still waits for it.
		/// </summary>
		Running,

		/// <summary>
		/// The user left the process running: the tool call has already returned, the process has not.
		/// </summary>
		Left,

		/// <summary>
		/// The process exited on its own and its exit code is known.
		/// </summary>
		Completed,

		/// <summary>
		/// The process was killed on purpose - by the card, by the call's cancellation or by the user
		/// leaving it and killing it later. There is no meaningful exit code, only the fact of the kill.
		/// </summary>
		Killed,

		/// <summary>
		/// The process could not be launched or its session is unavailable.
		/// </summary>
		Failed
	}
}
