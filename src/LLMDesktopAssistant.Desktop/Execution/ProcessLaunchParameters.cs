using System.Collections.Immutable;

namespace LLMDesktopAssistant.Desktop.Execution
{
	public class ProcessLaunchParameters
	{
		/// <summary>
		/// The display name of the process to be launched. Used for identifying by the user in the dispatcher.
		/// </summary>
		public required string? ProcessName { get; init; }

		/// <summary>
		/// The name of the file to execute.
		/// </summary>
		public required string FileName { get; init; }

		/// <summary>
		/// A list of arguments to pass to the file.
		/// </summary>
		public required ImmutableList<string> Arguments { get; init; }

		/// <summary>
		/// Whether to pass arguments verbatim (without escaping). This is useful for passing file paths that may contain spaces or special characters.
		/// </summary>
		public bool VerbatimArguments { get; init; } = false;

		/// <summary>
		/// The working directory for the process.
		/// </summary>
		public required string WorkingDirectory { get; init; }

		/// <summary>
		/// Whether to run process in the interactive terminal.
		/// </summary>
		public bool RunInTerminal { get; init; } = false;

		/// <summary>
		/// The environment variables to set for the process.
		/// </summary>
		public ImmutableDictionary<string, string?> EnvironmentVariables { get; init; } = [];

		/// <summary>
		/// The initial standard input to pass to the process.
		/// </summary>
		public string? StdIn { get; init; }

		/// <summary>
		/// The time span after which the process should be terminated if it has not completed.
		/// </summary>
		public TimeSpan? TimeOut { get; init; } = TimeSpan.FromMinutes(30);

		/// <summary>
		/// The time span after which the process should be removed from the dispatcher.
		/// If null - it will not be removed. If zero - it will be removed immediately upon completion.
		/// </summary>
		public TimeSpan? CompletionExpiryTime { get; init; } = TimeSpan.FromMinutes(5);

		/// <summary>
		/// Creates a copy of these parameters with a different timeout.
		/// </summary>
		/// <param name="timeout">The new timeout, or <see langword="null"/> to run without one.</param>
		public ProcessLaunchParameters WithTimeout(TimeSpan? timeout) => new()
		{
			ProcessName = ProcessName,
			FileName = FileName,
			Arguments = Arguments,
			VerbatimArguments = VerbatimArguments,
			WorkingDirectory = WorkingDirectory,
			RunInTerminal = RunInTerminal,
			EnvironmentVariables = EnvironmentVariables,
			StdIn = StdIn,
			TimeOut = timeout,
			CompletionExpiryTime = CompletionExpiryTime
		};
	}
}
