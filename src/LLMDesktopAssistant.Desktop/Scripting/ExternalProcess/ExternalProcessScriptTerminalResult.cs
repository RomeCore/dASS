namespace LLMDesktopAssistant.Desktop.Scripting.ExternalProcess
{
	/// <summary>
	/// The outcome of one terminal invocation.
	/// </summary>
	public sealed record ExternalProcessScriptTerminalResult
	{
		public required ExternalProcessStatus Status { get; init; }

		public int? ExitCode { get; init; }

		public required string Output { get; init; }

		public string? Error { get; init; }
	}
}
