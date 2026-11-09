using System.Runtime.CompilerServices;

namespace LLMDesktopAssistant.Desktop.Execution
{
	public class ProcessDescriptor : NotifyPropertyChanged
	{
		/// <summary>
		/// The unique identifier for the process. This is not necessarily the same as the OS process ID.
		/// </summary>
		public required Guid Id { get; init; }

		/// <summary>
		/// The unique identifier for the process in the OS.
		/// </summary>
		public required int ProcessId { get; init; }

		/// <summary>
		/// The parameters used to launch the process.
		/// </summary>
		public required ProcessLaunchParameters LaunchParameters { get; init; }

		/// <summary>
		/// The cancellation token source used to kill the process.
		/// </summary>
		public required CancellationTokenSource CancellationTokenSource { get; init; }

		/// <summary>
		/// The task that will be completed when the process exits, returning the exit code of the process.
		/// </summary>
		public required Task<int> ExitCodeTask { get; init; }

		/// <summary>
		/// The terminal session associated with this process, if process ran with <see cref="ProcessLaunchParameters.RunInTerminal"/> set to true.
		/// </summary>
		public required ProcessTerminalSession? TerminalSession { get; init; }

		/// <summary>
		/// The output associated with this process, if process ran with <see cref="ProcessLaunchParameters.RunInTerminal"/> set to false.
		/// </summary>
		public required ProcessOutput? PlainOutput { get; init; }

		/// <summary>
		/// The writable standard input of a plain process, or <see langword="null"/> when it was not redirected.
		/// </summary>
		public ProcessStandardInput? StandardInput { get; init; }

		/// <summary>
		/// The output associated with this process, either from a terminal session or plain output.
		/// </summary>
		public string Output => TerminalSession?.Output ?? string.Join(Environment.NewLine, PlainOutput!.Output);

		private int _exitCode = -1;
		/// <summary>
		/// Exit code of the process. -1 if not yet known.
		/// </summary>
		public int ExitCode
		{
			get => _exitCode;
			internal set => SetProperty(ref _exitCode, value);
		}

		private ProcessStatus _status = ProcessStatus.Pending;
		/// <summary>
		/// The current status of the process.
		/// </summary>
		public ProcessStatus Status
		{
			get => _status;
			internal set => SetProperty(ref _status, value);
		}

		private bool _isRunning = true;
		/// <summary>
		/// Indicates whether the process is currently running.
		/// </summary>
		public bool IsRunning
		{
			get => _isRunning;
			internal set => SetProperty(ref _isRunning, value);
		}

		private Exception? _exception = null;
		/// <summary>
		/// The exception that occurred while running the process, if any.
		/// </summary>
		public Exception? Exception
		{
			get => _exception;
			internal set => SetProperty(ref _exception, value);
		}

		/// <summary>
		/// Gets a task awaiter for the exit code of the process.
		/// </summary>
		/// <returns>A task awaiter that can be used to asynchronously wait for the exit code of the process.</returns>
		public TaskAwaiter<int> GetAwaiter() => ExitCodeTask.GetAwaiter();
	}
}
