using System.Text.Json.Nodes;
using LLMDesktopAssistant.Controls.Icons;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.MVVM.Additional;
using LLMDesktopAssistant.Tools;
using LLMDesktopAssistant.Tools.Consents;
using LLMDesktopAssistant.Tools.Execution.Domain;

namespace LLMDesktopAssistant.Agents.Tasks
{
	/// <summary>
	/// The agent-task world's tool call. In-memory only; the neutral state bag projected onto the
	/// task's own result and message plumbing.
	/// </summary>
	public class AgentToolCall : NotifyPropertyChanged, IToolCall
	{
		private ToolStatus _status = ToolStatus.Pending;
		/// <summary>
		/// Gets or sets the status of the tool call.
		/// </summary>
		public ToolStatus Status
		{
			get => _status;
			set => SetProperty(ref _status, value);
		}

		/// <summary>
		/// The tool call ID. This is a unique identifier for the tool call.
		/// </summary>
		public required string ToolCallId { get; init; }

		/// <summary>
		/// The name/identifier of the tool being called.
		/// </summary>
		public required string ToolName { get; init; }

		private string _arguments = string.Empty;
		/// <summary>
		/// The arguments passed to the tool.
		/// </summary>
		public string Arguments
		{
			get => _arguments;
			set => SetProperty(ref _arguments, value);
		}

		private AgentToolCallResult? _result;
		/// <summary>
		/// The result of the tool call. Kept until the task path is re-expressed on the shared core;
		/// afterwards the neutral members below are the source of truth.
		/// </summary>
		public AgentToolCallResult? Result
		{
			get => _result;
			set => SetProperty(ref _result, value);
		}

		private ToolBehaviour _expectedBehaviour;
		/// <summary>
		/// The expected behaviour flags for this tool call, determined during pre-execution.
		/// Used to display behaviour indicators in the confirmation UI.
		/// </summary>
		public ToolBehaviour ExpectedBehaviour
		{
			get => _expectedBehaviour;
			set => SetProperty(ref _expectedBehaviour, value);
		}

		private string? _resultContent;
		/// <summary>
		/// The content the tool produced.
		/// </summary>
		public string? ResultContent
		{
			get => _resultContent;
			set => SetProperty(ref _resultContent, value);
		}

		private bool _useMarkdown;
		/// <summary>
		/// Whether the result content is Markdown.
		/// </summary>
		public bool UseMarkdown
		{
			get => _useMarkdown;
			set => SetProperty(ref _useMarkdown, value);
		}

		private JsonNode? _structuredResult;
		/// <summary>
		/// An optional structured result for APIs that consume it.
		/// </summary>
		public JsonNode? StructuredResult
		{
			get => _structuredResult;
			set => SetProperty(ref _structuredResult, value);
		}

		private VisualIconKind? _statusIcon;
		/// <summary>
		/// The icon shown next to the call title.
		/// </summary>
		public VisualIconKind? StatusIcon
		{
			get => _statusIcon;
			set => SetProperty(ref _statusIcon, value);
		}

		private string? _statusTitle;
		/// <summary>
		/// The status title shown next to the call title.
		/// </summary>
		public string? StatusTitle
		{
			get => _statusTitle;
			set => SetProperty(ref _statusTitle, value);
		}

		private TaskCompletionSource<ToolConsentResult>? _userConfirmationSource;
		/// <summary>
		/// The pending confirmation the task card resolves. Runtime-only.
		/// </summary>
		public TaskCompletionSource<ToolConsentResult>? UserConfirmationSource
		{
			get => _userConfirmationSource;
			set => SetProperty(ref _userConfirmationSource, value);
		}

		private CancellationTokenSource _cancellationTokenSource = new();
		/// <summary>
		/// The per-invocation cancellation source. Runtime-only.
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

		private readonly List<AgentAttachment> _attachments = [];
		/// <summary>
		/// The attachments the tool produced, projected into the task's native form.
		/// </summary>
		public IReadOnlyList<AgentAttachment> Attachments => _attachments;

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
		/// Adds an attachment, projected into the task's native form.
		/// </summary>
		/// <param name="attachment">The neutral attachment to project.</param>
		public void AddAttachment(ToolAttachment attachment)
		{
			var type = attachment.Kind switch
			{
				ToolAttachmentKind.Image => AgentAttachmentType.Image,
				ToolAttachmentKind.Audio => AgentAttachmentType.Audio,
				ToolAttachmentKind.Video => AgentAttachmentType.Video,
				_ => throw new ArgumentOutOfRangeException(nameof(attachment), attachment.Kind, "Unknown attachment kind.")
			};

			_attachments.Add(new AgentAttachment { Type = type, Url = attachment.Url });
		}

		/// <summary>
		/// The task keeps only the native attachment part of the added data; everything else is a chat
		/// concern and is dropped.
		/// </summary>
		public void AddAdditionalData(AdditionalChatData data)
		{
			if (data is not NativeAttachmentMessagePart { NativeAttachment: not null } part)
				return;

			var attachment = AgentAttachment.TryConvertFromNativeAttachment(part.NativeAttachment);
			if (attachment is not null)
				_attachments.Add(attachment);
		}
	}
}
