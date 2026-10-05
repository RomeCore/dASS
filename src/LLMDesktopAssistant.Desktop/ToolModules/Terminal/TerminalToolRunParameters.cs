using LLMDesktopAssistant.Desktop.Execution;
using LLMDesktopAssistant.Tools;
using LLMDesktopAssistant.Controls.Icons;
using Material.Icons;

namespace LLMDesktopAssistant.Desktop.ToolModules.Terminal
{
	/// <summary>
	/// Parameters for running a process in a terminal.
	/// </summary>
	public class TerminalToolRunParameters
	{
		/// <summary>
		/// The status icon to set into <see cref="ReactiveToolResult"/>.
		/// </summary>
		public VisualIconKind? StatusIcon { get; init; }

		/// <summary>
		/// The status title to set into <see cref="ReactiveToolResult"/>.
		/// </summary>
		public string? StatusTitle { get; init; }

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
