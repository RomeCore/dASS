using System.Text.Json.Nodes;
using LLMDesktopAssistant.Controls.Icons;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.Tools.Consents;
using LLMDesktopAssistant.Utils.Attachments;

namespace LLMDesktopAssistant.Tools.Execution.Domain
{
	/// <summary>
	/// The state of a single tool invocation. Fat but neutral: both worlds project their concrete call
	/// onto it, and the chat-only members stay on the concrete implementation.
	/// </summary>
	/// <remarks>
	/// This is the object the tool writes its output into. There is no completion handle and no result
	/// payload: the main executor's task is the completion, and everything it produces lands here.
	/// The confirmation TCS and the cancellation source are runtime-only and are never persisted.
	/// </remarks>
	public interface IToolCall
	{
		/// <summary>
		/// The name of the tool that is being called, as the model emitted it (after alias resolution).
		/// </summary>
		string ToolName { get; }

		/// <summary>
		/// The model's intent as raw JSON. Editing this at approval substitutes the intent and is
		/// persisted; the effective arguments are read through <see cref="IToolExecutionContext.Arguments"/>.
		/// </summary>
		string Arguments { get; set; }

		/// <summary>
		/// The lifecycle status of the call. The pipeline moves it through streaming, preview,
		/// confirmation, execution and settlement; the UI binds to it.
		/// </summary>
		ToolStatus Status { get; set; }

		/// <summary>
		/// The expected behaviour of the call, determined by preview or by the descriptor default. It is
		/// the input of the policy stage.
		/// </summary>
		ToolBehaviour? ExpectedBehaviour { get; set; }

		/// <summary>
		/// The content the tool produced. The chat prompt is built from this value on the next request,
		/// so a restart that rewrites it is visible to the model without a new turn.
		/// </summary>
		string? ResultContent { get; set; }

		/// <summary>
		/// Whether <see cref="ResultContent"/> is Markdown and should be rendered accordingly.
		/// </summary>
		bool UseMarkdown { get; set; }

		/// <summary>
		/// An optional structured result for APIs that consume tools outside the chat UI (MCP, the Lua
		/// API, the external process bridge), or <see langword="null"/>.
		/// </summary>
		JsonNode? StructuredResult { get; set; }

		/// <summary>
		/// The icon shown next to the call title while it runs. Streaming analytics and main execution
		/// may update it.
		/// </summary>
		VisualIconKind? StatusIcon { get; set; }

		/// <summary>
		/// The status title shown next to the call title while it runs, or <see langword="null"/>.
		/// </summary>
		string? StatusTitle { get; set; }

		/// <summary>
		/// The pending confirmation the UI resolves. The confirmation service creates it, the consent
		/// panel completes it. Runtime-only.
		/// </summary>
		TaskCompletionSource<ToolConsentResult>? UserConfirmationSource { get; set; }

		/// <summary>
		/// The per-invocation cancellation source. Runtime-only; it is linked with the run token when the
		/// invocation starts and replaced before a restart, so the UI can cancel one call without
		/// touching its siblings.
		/// </summary>
		CancellationTokenSource CancellationTokenSource { get; set; }

		/// <summary>
		/// Adds a payload the call projects into its world's native form: a UI part is stored as is, a
		/// native or content attachment becomes the corresponding invisible part. The payload is an
		/// argument, not stored neutral state.
		/// </summary>
		/// <param name="attachment">The payload to add.</param>
		void AddAttachment(IChatAttachment attachment);

		/// <summary>
		/// Reports progress together with the range it belongs to.
		/// </summary>
		/// <param name="value">The current value, or <see langword="null"/> for indeterminate progress.</param>
		/// <param name="min">The minimum of the range.</param>
		/// <param name="max">The maximum of the range.</param>
		void SetProgress(double? value, double min, double max);
	}
}
