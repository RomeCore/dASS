namespace LLMDesktopAssistant.Tools.Execution.Domain
{
	/// <summary>
	/// The main executor's return. It says only whether the tool succeeded; everything else lives on
	/// the call.
	/// </summary>
	public sealed class ToolExecutionResult
	{
		/// <summary>
		/// Whether the tool succeeded.
		/// </summary>
		public required bool Success { get; init; }

		/// <summary>
		/// A successful result. Safe to share: the result is immutable.
		/// </summary>
		public static ToolExecutionResult Succeeded { get; } = new() { Success = true };

		/// <summary>
		/// A failed result. Safe to share: the result is immutable.
		/// </summary>
		public static ToolExecutionResult Failed { get; } = new() { Success = false };
	}
}
