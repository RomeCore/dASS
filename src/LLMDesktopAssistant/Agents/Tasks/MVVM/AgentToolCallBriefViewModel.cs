using CommunityToolkit.Mvvm.Input;
using LLMDesktopAssistant.LLM.MVVM.Messages;
using LLMDesktopAssistant.LLM.MVVM.Settings;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Tools;
using LLMDesktopAssistant.Tools.Consents;
using LLMDesktopAssistant.Utils;
using Material.Icons;
using ToolStatus = LLMDesktopAssistant.LLM.Domain.ToolStatus;

namespace LLMDesktopAssistant.Agents.Tasks.MVVM
{
	/// <summary>
	/// View model for a single <see cref="AgentToolCall"/>, providing UI-friendly bindings
	/// for status, arguments, result, behaviour flags, and confirmation workflow.
	/// </summary>
	[ViewModelFor(typeof(AgentToolCallBriefView))]
	public class AgentToolCallBriefViewModel : ViewModelBase
	{
		private readonly AgentToolCall _toolCall;
		private readonly AgentTask _parentTask;

		/// <summary>
		/// The underlying <see cref="AgentToolCall"/> model.
		/// </summary>
		public AgentToolCall ToolCall => _toolCall;

		/// <summary>
		/// The name of the tool being called.
		/// </summary>
		public string ToolName => _toolCall.ToolName;

		/// <summary>
		/// The display name of the tool being called.
		/// </summary>
		public string ToolDisplayName => ResolveDisplayName(
			_parentTask.LaunchParameters.Tools.FirstOrDefault(t => t.Name == _toolCall.ToolName)) ?? ToolName;

		/// <summary>
		/// Resolves the human-readable name of a task tool. The task toolset holds both worlds, and only
		/// the concrete descriptors know their display name.
		/// </summary>
		/// <param name="descriptor">The descriptor, or <see langword="null"/> when the tool is gone.</param>
		/// <returns>The display name, or <see langword="null"/>.</returns>
		private static string? ResolveDisplayName(LLMDesktopAssistant.Tools.Execution.Domain.IToolDescriptor? descriptor) =>
			descriptor switch
			{
				ToolInfo info => info.NameKey.Value,
				AgentTool taskTool => taskTool.DisplayName,
				_ => descriptor?.Name
			};

		private string _arguments = string.Empty;
		/// <summary>
		/// The formatted arguments of the tool call, ready for display.
		/// </summary>
		public string Arguments
		{
			get => _arguments;
			private set => SetProperty(ref _arguments, value);
		}

		private string? _result;
		/// <summary>
		/// The result content of the tool call, if available.
		/// </summary>
		public string? Result
		{
			get => _result;
			private set => SetProperty(ref _result, value);
		}

		private bool _hasResult;
		/// <summary>
		/// Whether the tool call has a result to display.
		/// </summary>
		public bool HasResult
		{
			get => _hasResult;
			private set => SetProperty(ref _hasResult, value);
		}

		private VisualIconKind _statusIcon = MaterialIconKind.HelpCircle;
		/// <summary>
		/// The icon representing the current status of the tool call.
		/// </summary>
		public VisualIconKind StatusIcon
		{
			get => _statusIcon;
			private set => SetProperty(ref _statusIcon, value);
		}

		private string _statusText = string.Empty;
		/// <summary>
		/// Localized text describing the current status of the tool call.
		/// </summary>
		public string StatusText
		{
			get => _statusText;
			private set => SetProperty(ref _statusText, value);
		}

		private bool _isConfirming;
		/// <summary>
		/// Whether the tool call is waiting for user confirmation.
		/// </summary>
		public bool IsConfirming
		{
			get => _isConfirming;
			private set => SetProperty(ref _isConfirming, value);
		}

		private bool _isRunning;
		/// <summary>
		/// Whether the tool call is currently executing (pre-executing or executing).
		/// </summary>
		public bool IsRunning
		{
			get => _isRunning;
			private set => SetProperty(ref _isRunning, value);
		}

		private bool _isCompleted;
		/// <summary>
		/// Whether the tool call has reached a terminal state.
		/// </summary>
		public bool IsCompleted
		{
			get => _isCompleted;
			private set => SetProperty(ref _isCompleted, value);
		}

		private bool _isFailed;
		/// <summary>
		/// Whether the tool call has failed.
		/// </summary>
		public bool IsFailed
		{
			get => _isFailed;
			private set => SetProperty(ref _isFailed, value);
		}

		/// <summary>
		/// The behaviour flags for this tool call to display as indicators.
		/// </summary>
		public ImmutableList<ToolBehaviourFlagInfo> BehaviourFlags =>
			ToolBehaviourFlagInfo.CreateForFlags(_toolCall.ExpectedBehaviour);

		/// <summary>
		/// Whether to show the standard confirmation controls.
		/// </summary>
		public bool ShowConfirmButtons => IsConfirming;

		/// <summary>
		/// Gets the command that resolves the tool confirmation with a <see cref="ToolConsentResult"/>.
		/// Executed by the <see cref="ToolConsentPanel"/>.
		/// </summary>
		public ICommand ConsentCommand { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="AgentToolCallBriefViewModel"/> class.
		/// </summary>
		/// <param name="toolCall">The <see cref="AgentToolCall"/> to wrap.</param>
		/// <param name="parentTask">The parent <see cref="AgentTask"/> that owns this tool call,
		/// used to look up confirmation requests.</param>
		public AgentToolCallBriefViewModel(AgentToolCall toolCall, AgentTask parentTask)
		{
			_toolCall = toolCall;
			_parentTask = parentTask;

			ConsentCommand = new RelayCommand<ToolConsentResult>(ResolveConsent);

			SyncArguments();
			SyncResult();
			SyncStatus();
		}

		/// <summary>
		/// Subscribes to property changes on the underlying <see cref="AgentToolCall"/>.
		/// Must be called after construction to begin live updates.
		/// </summary>
		public void Subscribe()
		{
			_toolCall.PropertyChanged += OnToolCallPropertyChanged;
		}

		private void OnToolCallPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			InvokeUI(() =>
			{
				switch (e.PropertyName)
				{
					case nameof(AgentToolCall.Status):
						SyncStatus();
						break;

					case nameof(AgentToolCall.Arguments):
						SyncArguments();
						break;

					case nameof(AgentToolCall.Result):
						SyncResult();
						break;

					case nameof(AgentToolCall.ExpectedBehaviour):
						RaisePropertyChanged(nameof(BehaviourFlags));
						break;
				}
			});
		}

		private void SyncStatus()
		{
			var status = _toolCall.Status;

			IsCompleted = status is ToolStatus.Success or ToolStatus.Error or ToolStatus.Cancelled;
			IsRunning = status is ToolStatus.Pending or ToolStatus.PreExecuting or ToolStatus.Executing;
			IsConfirming = status == ToolStatus.WaitingForApproval;
			IsFailed = status is ToolStatus.Error or ToolStatus.Cancelled;

			(StatusIcon, StatusText) = status switch
			{
				ToolStatus.Pending => (MaterialIconKind.ClockOutline,
					LocalizationManager.LocalizeStatic("tool.call.status.pending")),
				ToolStatus.PreExecuting => (MaterialIconKind.WrenchClock,
					LocalizationManager.LocalizeStatic("tool.call.status.pre_executing")),
				ToolStatus.WaitingForApproval => (MaterialIconKind.QuestionMarkCircle,
					LocalizationManager.LocalizeStatic("tool.call.status.confirming")),
				ToolStatus.Executing => (MaterialIconKind.WrenchClock,
					LocalizationManager.LocalizeStatic("tool.call.status.executing")),
				ToolStatus.Success => (MaterialIconKind.CheckCircle,
					LocalizationManager.LocalizeStatic("tool.call.status.success")),
				ToolStatus.Error => (MaterialIconKind.AlertCircle,
					LocalizationManager.LocalizeStatic("tool.call.status.failed")),
				ToolStatus.Cancelled => (MaterialIconKind.Cancel,
					LocalizationManager.LocalizeStatic("tool.call.status.cancelled")),
				_ => (MaterialIconKind.HelpCircle,
					LocalizationManager.LocalizeStatic("tool.call.status.unknown"))
			};

			RaisePropertyChanged(nameof(ShowConfirmButtons));
		}

		private void SyncArguments()
		{
			try
			{
				var parsedArgs = TolerantJsonParser.Parse(_toolCall.Arguments);
				Arguments = ToolCallArgumentFormatter.FormatToMarkdown(parsedArgs);
			}
			catch
			{
				if (!string.IsNullOrEmpty(_toolCall.Arguments))
					Arguments = "```json\n" + _toolCall.Arguments + "\n```";
				else
					Arguments = string.Empty;
			}
		}

		private void SyncResult()
		{
			Result = _toolCall.Result?.Content;
			HasResult = !string.IsNullOrEmpty(Result);
		}

		private void ResolveConsent(ToolConsentResult? consentResult)
		{
			if (consentResult != null)
				FindConfirmationRequest()?.UserConfirmationSource.TrySetResult(consentResult);
		}

		private AgentToolCallConfirmationRequest? FindConfirmationRequest()
		{
			return _parentTask.ToolCallConfirmationRequests
				.FirstOrDefault(r => r.ToolCall == _toolCall);
		}

		/// <inheritdoc />
		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				_toolCall.PropertyChanged -= OnToolCallPropertyChanged;
			}

			base.Dispose(disposing);
		}
	}
}
