namespace LLMDesktopAssistant.Desktop.Execution
{
	/// <summary>
	/// How a terminal run ended: the exit code (once it is known), whether the user detached the process, and
	/// whether the call was cancelled.
	/// </summary>
	/// <param name="ExitCode">
	/// The exit code, or <see langword="null"/> when the call returned before the process exited.
	/// </param>
	/// <param name="Left">Whether the user detached the terminal and the process keeps running.</param>
	/// <param name="Cancelled">Whether the call was cancelled and the process killed.</param>
	public readonly record struct TerminalRunResult(int? ExitCode, bool Left, bool Cancelled);
}
