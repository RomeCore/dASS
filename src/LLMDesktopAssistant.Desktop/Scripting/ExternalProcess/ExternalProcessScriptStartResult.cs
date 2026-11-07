using LLMDesktopAssistant.Desktop.Execution;

namespace LLMDesktopAssistant.Desktop.Scripting.ExternalProcess
{
	/// <summary>
	/// The outcome of preparing and launching one script: the live process, or the reason nothing was started.
	/// </summary>
	/// <param name="Process">The launched process, or <see langword="null"/>.</param>
	/// <param name="LaunchParameters">The interpreter invocation, or <see langword="null"/> when the launch never happened.</param>
	/// <param name="Status">How the preparation ended.</param>
	/// <param name="Error">Why nothing was started, or <see langword="null"/>.</param>
	public sealed record ExternalProcessScriptStartResult(ProcessDescriptor? Process,
		ProcessLaunchParameters? LaunchParameters, ExternalProcessStatus Status, string? Error)
	{
		/// <summary>
		/// Whether a process was launched.
		/// </summary>
		public bool Started => Process is not null;
	}
}
