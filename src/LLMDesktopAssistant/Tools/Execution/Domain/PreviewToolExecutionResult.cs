namespace LLMDesktopAssistant.Tools.Execution.Domain
{
	/// <summary>
	/// The preview executor's control outcome. Status, content and payloads are written into the call;
	/// this type carries only what the pipeline needs to decide how to continue.
	/// </summary>
	public sealed class PreviewToolExecutionResult
	{
		/// <summary>
		/// When set, preview ends the invocation: success or failure is final and execution does not run.
		/// The clickable result content and additional data are already on the call.
		/// </summary>
		public bool? InterruptingSuccess { get; init; }

		/// <summary>
		/// Overrides the expected behaviour for the policy stage, or <see langword="null"/> to keep the
		/// descriptor default.
		/// </summary>
		public ToolBehaviour? ExpectedBehaviour { get; init; }

		/// <summary>
		/// A result that does not interrupt: the pipeline continues with policy and execution.
		/// </summary>
		public static PreviewToolExecutionResult Continue { get; } = new();
	}
}
