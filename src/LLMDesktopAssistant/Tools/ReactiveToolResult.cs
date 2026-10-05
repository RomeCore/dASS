using System.Text.Json.Nodes;
using LLMDesktopAssistant.LLM.MVVM.Additional;
using LLMDesktopAssistant.Utils;
using Material.Icons;
using RCLargeLanguageModels.Tools;

namespace LLMDesktopAssistant.Tools
{
	/// <summary>
	/// Represents the streaming, advanced reactive result of a tool execution.
	/// Supports brief status icons and progress updates, title updates, and streaming content.
	/// Also supports advanced human-in-the-loop features like forms, prompts and fast answers.
	/// </summary>
	public class ReactiveToolResult : NotifyPropertyChanged
	{
		private readonly TaskCompletionSource<bool> _completionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
		private readonly Lock _lock = new();

		/// <summary>
		/// The task that will be completed when the tool execution is done.
		/// Task value is true if the tool executed successfully, and false if it failed.
		/// </summary>
		public Task<bool> Completion => _completionSource.Task;

		private double? _progress = null;
		/// <summary>
		/// The progress of the tool execution. If null, progress is indeterminate.
		/// </summary>
		public double? Progress
		{
			get => _progress;
			set
			{
				lock (_lock)
					SetProperty(ref _progress, value);
			}
		}

		private double _minProgress = 0.0;
		/// <summary>
		/// The minimum progress of the tool execution. Defaults to 0.0.
		/// </summary>
		public double MinProgress
		{
			get => _minProgress;
			set
			{
				lock (_lock)
					SetProperty(ref _minProgress, value);
			}
		}

		private double _maxProgress = 1.0;
		/// <summary>
		/// The maximum progress of the tool execution. Defaults to 1.0.
		/// </summary>
		public double MaxProgress
		{
			get => _maxProgress;
			set
			{
				lock (_lock)
					SetProperty(ref _maxProgress, value);
			}
		}

		private VisualIconKind? _statusIcon;
		/// <summary>
		/// The status icon to be displayed. This will be shown next to the main title (that contains tool name).
		/// </summary>
		public VisualIconKind? StatusIcon
		{
			get => _statusIcon;
			set
			{
				lock (_lock)
					SetProperty(ref _statusIcon, value);
			}
		}

		private string? _statusTitle;
		/// <summary>
		/// The title of the status that will be shown next to the main title (that contains tool name).
		/// </summary>
		public string? StatusTitle
		{
			get => _statusTitle;
			set
			{
				lock (_lock)
					SetProperty(ref _statusTitle, value);
			}
		}

		/// <summary>
		/// The observable collection of lines that contains the streaming output of the tool.
		/// Each line can contain multiple lines of text, not only a single line.
		/// Perfect for long-running tools like Python package installation.
		/// </summary>
		public RangeObservableCollection<string> ResultContentLines { get; } = [];

		/// <summary>
		/// The combined string of all lines in <see cref="ResultContentLines"/>, joined by <see cref="Environment.NewLine"/>.
		/// </summary>
		public string ResultContent
		{
			get => string.Join(Environment.NewLine, ResultContentLines);
			set
			{
				ResultContentLines.Clear();
				ResultContentLines.Add(value);
			}
		}

		private bool _useMarkdown = false;
		/// <summary>
		/// Whether the result content should be treated as Markdown. If true, the UI will render it accordingly.
		/// </summary>
		public bool UseMarkdown
		{
			get => _useMarkdown;
			set => SetProperty(ref _useMarkdown, value);
		}

		/// <summary>
		/// The collection of additional view models (parts) of the tool result: attachments (images, videos, audios),
		/// status chips and any other message parts. They are copied to the tool call that executed this result.
		/// </summary>
		public AdditionalChatDataCollection AdditionalData { get; } = [];

		private JsonNode? _structuredResult = null;
		/// <summary>
		/// Gets or sets the optional structured result. Usable for external APIs that calls tools, like MCP, Lua API, dASS RPC API (that used by external processes like Python).
		/// </summary>
		public JsonNode? StructuredResult
		{
			get => _structuredResult;
			set
			{
				lock (_lock)
					SetProperty(ref _structuredResult, value);
			}
		}

		public ReactiveToolResult()
		{
			ResultContentLines.CollectionChanged += (s, e) => RaisePropertyChanged(nameof(ResultContent));
		}

		/// <summary>
		/// Completes the task with a result indicating whether the tool executed successfully.
		/// </summary>
		/// <param name="success">Whether the tool executed successfully.</param>
		/// <exception cref="InvalidOperationException">Thrown if the task has already been completed.</exception>
		public ReactiveToolResult Complete(bool success)
		{
			lock (_lock)
				if (!_completionSource.TrySetResult(success))
					throw new InvalidOperationException("Tool execution already completed.");
			return this;
		}

		/// <summary>
		/// Completes the task with a 'success' result.
		/// </summary>
		/// <exception cref="InvalidOperationException">Thrown if the task has already been completed.</exception>
		public ReactiveToolResult CompleteWithSuccess()
		{
			return Complete(true);
		}

		/// <summary>
		/// Completes the task with an 'error' result.
		/// </summary>
		/// <exception cref="InvalidOperationException">Thrown if the task has already been completed.</exception>
		public ReactiveToolResult CompleteWithError()
		{
			return Complete(false);
		}

		/// <summary>
		/// Completes the task with a result indicating whether the tool executed successfully.
		/// </summary>
		/// <param name="success">Whether the tool executed successfully.</param>
		public ReactiveToolResult TryComplete(bool success)
		{
			lock (_lock)
				_completionSource.TrySetResult(success);
			return this;
		}

		/// <summary>
		/// Completes the task with a 'success' result.
		/// </summary>
		public ReactiveToolResult TryCompleteWithSuccess()
		{
			return TryComplete(true);
		}

		/// <summary>
		/// Completes the task with an 'error' result.
		/// </summary>
		public ReactiveToolResult TryCompleteWithError()
		{
			return TryComplete(false);
		}

		/// <summary>
		/// Creates a new instance of the <see cref="ReactiveToolResult"/> class with a success status and the specified output.
		/// </summary>
		/// <param name="result">The output of the tool.</param>
		/// <returns>A newly created <see cref="ReactiveToolResult"/>.</returns>
		public static ReactiveToolResult Create(bool success, string result)
		{
			var resultObj = new ReactiveToolResult();
			resultObj.ResultContent = result;
			resultObj.Complete(success: success);
			return resultObj;
		}

		/// <summary>
		/// Creates a new instance of the <see cref="ReactiveToolResult"/> class with a success status and the specified output.
		/// </summary>
		/// <param name="result">The output of the tool.</param>
		/// <returns>A newly created <see cref="ReactiveToolResult"/>.</returns>
		public static ReactiveToolResult CreateSuccess(string result)
		{
			return Create(true, result);
		}

		/// <summary>
		/// Creates a new instance of the <see cref="ReactiveToolResult"/> class with an error status and the specified output.
		/// </summary>
		/// <param name="result">The output of the tool.</param>
		/// <returns>A newly created <see cref="ReactiveToolResult"/>.</returns>
		public static ReactiveToolResult CreateError(string result)
		{
			return Create(false, result);
		}

		/// <summary>
		/// Creates a new instance of the <see cref="ReactiveToolResult"/> class from a <see cref="ToolResult"/>.
		/// </summary>
		/// <param name="result">The <see cref="ToolResult"/> to create the instance from.</param>
		/// <returns>A newly created <see cref="ReactiveToolResult"/>.</returns>
		public static ReactiveToolResult CreateFromResult(ToolResult result)
		{
			var resultObj = new ReactiveToolResult();
			resultObj.ResultContent = result.Content;

			var success = result.Status switch
			{
				ToolResultStatus.Success => true,
				ToolResultStatus.Error => false,
				ToolResultStatus.Cancelled => false,
				ToolResultStatus.NoResult => false,
				_ => false
			};

			resultObj.Complete(success: success);
			return resultObj;
		}
	}
}