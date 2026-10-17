using System.Text.Json.Nodes;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Tools;
using LLMDesktopAssistant.Tools.Consents;
using LLMDesktopAssistant.Tools.Execution.Domain;
using Material.Icons;
using RCLargeLanguageModels.Tasks;

namespace LLMDesktopAssistant.LLM.Domain
{
	/// <summary>
	/// Represents a tool call within a chat message.
	/// </summary>
	public class ToolCall : ChatObjectBase, IToolCall
	{
		/// <summary>
		/// Gets or sets the name of the tool being called.
		/// </summary>
		public required string ToolName { get; init; }

		/// <summary>
		/// Gets or sets the title of the tool call, which can be localized.
		/// </summary>
		public LocaleKeyBase? Title { get; init; }

		/// <summary>
		/// Gets or sets the LLM API-specific tool call ID that is used by the LLM
		/// to correctly bind each tool call to the tool result message.
		/// </summary>
		public required string ToolCallId { get; init; }

		/// <summary>
		/// Gets or sets the arguments passed to the tool. These are typically in JSON format.
		/// </summary>
		public string Arguments
		{
			get => field;
			set => SetProperty(ref field, value);
		} = "{}";

		private ToolStatus _status;
		/// <summary>
		/// Gets or sets the status of the tool call.
		/// </summary>
		public ToolStatus Status
		{
			get => _status;
			set => SetProperty(ref _status, value);
		}

		private VisualIconKind? _statusIcon;
		/// <summary>
		/// The status icon to be displayed. This will be shown next to the main title (that contains tool name).
		/// </summary>
		public VisualIconKind? StatusIcon
		{
			get => _statusIcon;
			set => SetProperty(ref _statusIcon, value);
		}

		private string? _statusTitle;
		/// <summary>
		/// The title of the status that will be shown next to the main title (that contains tool name).
		/// </summary>
		public string? StatusTitle
		{
			get => _statusTitle;
			set => SetProperty(ref _statusTitle, value);
		}

		private string? _resultContent = string.Empty;
		/// <summary>
		/// Gets or sets the content of the result returned by the tool.
		/// </summary>
		public string? ResultContent
		{
			get => _resultContent;
			set => SetProperty(ref _resultContent, value);
		}

		/// <summary>
		/// Gets or sets a value indicating whether the tool call result can be compacted.
		/// </summary>
		public bool CanBeCompacted
		{
			get => field;
			set => SetProperty(ref field, value);
		}

		private bool _useMarkdown = false;
		/// <summary>
		/// Gets or sets a value indicating whether the result content should be displayed using Markdown formatting.
		/// </summary>
		public bool UseMarkdown
		{
			get => _useMarkdown;
			set => SetProperty(ref _useMarkdown, value);
		}

		private JsonNode? _structuredResult = null;
		/// <summary>
		/// Gets or sets the optional structured result. Usable for external APIs that calls tools, like MCP, Lua API, dASS RPC API (that used by external processes like Python).
		/// </summary>
		public JsonNode? StructuredResult
		{
			get => _structuredResult;
			set => SetProperty(ref _structuredResult, value);
		}

		private TaskCompletionSource<ToolConsentResult>? _userConfirmationSource;
		/// <summary>
		/// Gets or sets the task completion source for user interaction.
		/// The result is null when user confirms this tool call, and this should be executed.
		/// The result is empty when user cancels tool call without a reason.
		/// The result is non-empty when user cancels tool call with a reason (and reason is provided as the result).
		/// </summary>
		public TaskCompletionSource<ToolConsentResult>? UserConfirmationSource
		{
			get => _userConfirmationSource;
			set => SetProperty(ref _userConfirmationSource, value);
		}

		private ToolBehaviour? _expectedBehaviour;
		/// <summary>
		/// Gets or sets the expected behaviour of the tool call.
		/// </summary>
		public ToolBehaviour? ExpectedBehaviour
		{
			get => _expectedBehaviour;
			set => SetProperty(ref _expectedBehaviour, value);
		}

		ToolBehaviour IToolCall.ExpectedBehaviour
		{
			get => ExpectedBehaviour ?? ToolBehaviour.None;
			set => ExpectedBehaviour = value;
		}

		private CancellationTokenSource _cancellationTokenSource = new();
		/// <summary>
		/// The per-invocation cancellation source. Runtime-only: it is never persisted and is replaced
		/// before a restart.
		/// </summary>
		public CancellationTokenSource CancellationTokenSource
		{
			get => _cancellationTokenSource;
			set => SetProperty(ref _cancellationTokenSource, value);
		}

		private double? _progress;
		/// <summary>
		/// The progress of the tool execution. <see langword="null"/> means indeterminate.
		/// </summary>
		public double? Progress
		{
			get => _progress;
			private set => SetProperty(ref _progress, value);
		}

		private double _minProgress;
		/// <summary>
		/// The minimum of the progress range.
		/// </summary>
		public double MinProgress
		{
			get => _minProgress;
			private set => SetProperty(ref _minProgress, value);
		}

		private double _maxProgress = 1.0;
		/// <summary>
		/// The maximum of the progress range.
		/// </summary>
		public double MaxProgress
		{
			get => _maxProgress;
			private set => SetProperty(ref _maxProgress, value);
		}

		/// <summary>
		/// Reports progress together with the range it belongs to.
		/// </summary>
		/// <param name="value">The current value, or <see langword="null"/> for indeterminate progress.</param>
		/// <param name="min">The minimum of the range.</param>
		/// <param name="max">The maximum of the range.</param>
		public void SetProgress(double? value, double min, double max)
		{
			MinProgress = min;
			MaxProgress = max;
			Progress = value;
		}

		/// <summary>
		/// Gets or sets the completion token associated with this tool call.
		/// </summary>
		public required CompletionToken CompletionToken { get; init; }

		/// <summary>
		/// Gets a value indicating whether the tool call has been completed (e.g. not processing or pending).
		/// This means this tool call has finished or been loaded from database.
		/// </summary>
		public bool IsCompleted => CompletionToken.IsCompleted;

		public CompletionToken GetAwaiter() => CompletionToken;

		protected override void Dispose(bool disposing)
		{
			if (disposing)
				_cancellationTokenSource.Dispose();

			base.Dispose(disposing);
		}
	}
}