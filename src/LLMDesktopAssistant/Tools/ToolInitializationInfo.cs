using System.Text.Json.Nodes;

namespace LLMDesktopAssistant.Tools
{
	/// <summary>
	/// The initialization information for a <see cref="ToolInfo"/>. Every executor slot is a plain
	/// delegate; <see cref="ToolInfo.Create"/> binds each of them through the
	/// <see cref="Execution.Executors.ToolMethodBinder"/>.
	/// </summary>
	/// <remarks>
	/// The slots are delegates rather than already-built executors so a tool module declares its
	/// capabilities as ordinary methods, with the same argument binding and schema generation as its
	/// main method.
	/// </remarks>
	public class ToolInitializationInfo : ToolInfo
	{
		/// <summary>
		/// The main executor delegate. Required.
		/// </summary>
		public required Delegate Executor { get; init; }

		/// <summary>
		/// The streaming analyzer delegate, invoked on every update of the tool call arguments.
		/// </summary>
		public Delegate? StreamingAnalyzer { get; init; }

		/// <summary>
		/// The preview executor delegate, invoked before the main executor and the specifier analyzer.
		/// Used to determine the tool's behaviour and to short-circuit when the arguments are not valid
		/// semantically (for example, a file to delete does not exist).
		/// </summary>
		public Delegate? PreviewExecutor { get; init; }

		/// <summary>
		/// The specifier analyzer delegate, used to check the tool call arguments against a specifier.
		/// </summary>
		public Delegate? SpecifierAnalyzer { get; init; }

		/// <summary>
		/// The synchronization analyzer delegate, used to derive per-invocation synchronization keys
		/// from the arguments.
		/// </summary>
		public new Delegate? SyncAnalyzer { get; init; }

		/// <summary>
		/// The tool's own approval interface delegate, or <see langword="null"/> when the generic consent
		/// panel handles the decision. The delegate returns <see cref="Execution.Domain.ToolApprovalResult"/>.
		/// </summary>
		public new Delegate? Approval { get; init; }

		/// <summary>
		/// The schema of the data the approval surface may request from the user, when
		/// <see cref="Approval"/> is set; otherwise <see langword="null"/>.
		/// </summary>
		public JsonObject? ApprovalRequestedDataSchema { get; init; }

		/// <summary>
		/// The action to modify the generated argument schema before the tool is frozen.
		/// </summary>
		public Action<JsonObject>? ModifyArgumentSchema { get; init; }
	}
}
