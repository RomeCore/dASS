using LLMDesktopAssistant.Desktop.Execution;

namespace LLMDesktopAssistant.Desktop.ToolModules.Terminal
{
	/// <summary>
	/// Parameters for running a process in a terminal.
	/// </summary>
	public class TerminalToolRunParameters
	{
		/// <summary>
		/// Whether to wait for the process to complete before returning.
		/// </summary>
		public bool Wait { get; init; } = true;

		/// <summary>
		/// The parameters for launching the process.
		/// </summary>
		public required ProcessLaunchParameters ProcessParameters { get; init; }
	}
}
