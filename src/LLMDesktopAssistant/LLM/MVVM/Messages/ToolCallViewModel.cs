using System.ComponentModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using LLMDesktopAssistant.Controls.Markdown;
using LLMDesktopAssistant.Controls.Markdown.CodeBlocks;
using LLMDesktopAssistant.Controls.Markdown.Selection;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.MVVM.Additional;
using LLMDesktopAssistant.LLM.MVVM.Settings;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Tools;
using LLMDesktopAssistant.Tools.Consents;
using LLMDesktopAssistant.Tools.Execution;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Utils;
using Material.Icons;
using Microsoft.Extensions.DependencyInjection;

namespace LLMDesktopAssistant.LLM.MVVM.Messages
{
	[ViewModelFor(typeof(ToolCallView))]
	public class ToolCallViewModel : ViewModelBase
	{
		private readonly ChatMessage _message;
		private readonly Chat _chat;

		private IChatToolCallService? _toolCallService;
		private IChatToolCallService ToolCallService =>
			_toolCallService ??= _chat.Services.GetRequiredService<IChatToolCallService>();

		public ToolCall ToolCall { get; }

		public string ToolName { get; }

		public LocaleKeyBase ToolTitle { get; }

		public string ToolCallId { get; }

		/// <summary>
		/// The additional data of the tool call (chips + everything else). Always created.
		/// </summary>
		public AdditionalChatDataCollectionViewModel AdditionalData { get; }

		private MessageAlignment _alignment = MessageAlignment.Left;
		/// <summary>
		/// Gets the alignment of the tool call, derived from the role of the message that contains it.
		/// Set by <see cref="ToolCallListViewModel"/>; forwarded to the tool call's own additional data.
		/// </summary>
		public MessageAlignment Alignment
		{
			get => _alignment;
			set
			{
				_alignment = value;
				AdditionalData.Alignment = value;
			}
		}

		private ToolCallFlyoutViewModel? _flyout;
		/// <summary>
		/// Gets the lazily-created view model backing the tool call flyout.
		/// </summary>
		public ToolCallFlyoutViewModel Flyout => _flyout ??= new ToolCallFlyoutViewModel(this);

		private IMarkdownSelectionProcessorsProvider? _selectionProcessorsProvider;
		/// <summary>
		/// Gets the actions the markdown selection toolbar offers for this tool call's content.
		/// </summary>
		public IMarkdownSelectionProcessorsProvider SelectionProcessorsProvider =>
			_selectionProcessorsProvider ??= MarkdownSelectionProcessorProviders.Resolve(
				_chat.Services, MarkdownExtensionScope.ToolCall);

		private IReadOnlyList<ICodeBlockPreviewProvider>? _codeBlockPreviewProviders;
		/// <summary>
		/// Gets the preview tabs the code blocks of this tool call offer, resolved for the tool call surface.
		/// </summary>
		public IReadOnlyList<ICodeBlockPreviewProvider> CodeBlockPreviewProviders =>
			_codeBlockPreviewProviders ??= MarkdownExtensionProviders.Resolve<ICodeBlockPreviewProvider>(
				_chat.Services, MarkdownExtensionScope.ToolCall);

		private IReadOnlyList<IMarkdownCodeBlockActionProvider>? _codeBlockActionProviders;
		/// <summary>
		/// Gets the buttons the code blocks of this tool call offer (copy, save, ...), resolved for the tool call surface.
		/// </summary>
		public IReadOnlyList<IMarkdownCodeBlockActionProvider> CodeBlockActionProviders =>
			_codeBlockActionProviders ??= MarkdownExtensionProviders.Resolve<IMarkdownCodeBlockActionProvider>(
				_chat.Services, MarkdownExtensionScope.ToolCall);

		private ToolStatus _status = ToolStatus.None;
		public ToolStatus Status
		{
			get => _status;
			set
			{
				if (SetProperty(ref _status, value))
				{
					RaisePropertyChanged(nameof(ToolIcon));
					RaisePropertyChanged(nameof(ToolIconBrush));
					RaisePropertyChanged(nameof(DisplayMiniIcon));
					RaisePropertyChanged(nameof(IsBlinking));
					RaisePropertyChanged(nameof(InProgress));
					RaisePropertyChanged(nameof(ToolIconVisible));
					RaisePropertyChanged(nameof(UserAsked));
				}
			}
		}

		private double? _progress = null;
		public double? Progress
		{
			get => _progress;
			set => SetProperty(ref _progress, value);
		}

		private double _minProgress = 0.0;
		public double MinProgress
		{
			get => _minProgress;
			set => SetProperty(ref _minProgress, value);
		}

		private double _maxProgress = 1.0;
		public double MaxProgress
		{
			get => _maxProgress;
			set => SetProperty(ref _maxProgress, value);
		}

		private ToolBehaviour? _expectedBehaviour = ToolBehaviour.None;
		public ToolBehaviour? ExpectedBehaviour
		{
			get => _expectedBehaviour;
			set
			{
				if (SetProperty(ref _expectedBehaviour, value))
				{
					RaisePropertyChanged(nameof(BehaviourFlags));
				}
			}
		}

		/// <summary>
		/// Gets the list of behaviour flags with icons and colors for display.
		/// </summary>
		public ImmutableList<ToolBehaviourFlagInfo> BehaviourFlags
		{
			get
			{
				if (_expectedBehaviour == null)
					return [];

				return ToolBehaviourFlagInfo.CreateForFlags(_expectedBehaviour.Value);
			}
		}

		private VisualIconKind? _statusIcon;
		public VisualIconKind? StatusIcon
		{
			get => _statusIcon;
			set => SetProperty(ref _statusIcon, value);
		}

		private string? _statusTitle;
		public string? StatusTitle
		{
			get => _statusTitle;
			set => SetProperty(ref _statusTitle, value);
		}

		public VisualIconKind ToolIcon =>
			Status switch
			{
				ToolStatus.Pending => MaterialIconKind.Edit,
				ToolStatus.PreExecuting => MaterialIconKind.WrenchClock,
				ToolStatus.Executing => MaterialIconKind.WrenchClock,
				ToolStatus.WaitingForApproval => MaterialIconKind.QuestionMarkCircle,
				ToolStatus.ExecutionInterrupted => MaterialIconKind.CancelCircle,
				ToolStatus.Success => MaterialIconKind.CheckCircle,
				ToolStatus.Cancelled => MaterialIconKind.CancelCircle,
				ToolStatus.Error => MaterialIconKind.AlertCircle,
				_ => MaterialIconKind.Wrench
			};

		public IBrush? ToolIconBrush =>
			Status switch
			{
				ToolStatus.Pending => (IBrush?)null,
				ToolStatus.PreExecuting => (IBrush?)null,
				ToolStatus.Executing => (IBrush?)null,
				ToolStatus.WaitingForApproval => (IBrush?)null,
				ToolStatus.ExecutionInterrupted => (IBrush?)null,
				ToolStatus.Success => (IBrush?)null,
				ToolStatus.Cancelled => (IBrush?)null,
				ToolStatus.Error => (IBrush?)null,
				_ => (IBrush?)null
			} ?? Brushes.White;

		public bool DisplayMiniIcon =>
			Status switch
			{
				ToolStatus.Success => false,
				_ => true
			};

		public bool IsBlinking =>
			Status switch
			{
				ToolStatus.Pending => true,
				_ => false
			};

		public bool InProgress =>
			Status switch
			{
				ToolStatus.PreExecuting => true,
				ToolStatus.Executing => true,
				_ => false
			};

		public bool ToolIconVisible =>
			Status switch
			{
				ToolStatus.PreExecuting => false,
				ToolStatus.Executing => false,
				_ => true
			};

		public bool UserAsked =>
			Status switch
			{
				ToolStatus.WaitingForApproval => ToolCall.UserConfirmationSource is not null,
				_ => false
			};

		/// <summary>
		/// Gets the command that resolves the tool confirmation with a <see cref="ToolApprovalResult"/>.
		/// Executed by the <see cref="ToolConsentPanel"/>.
		/// </summary>
		public ICommand ConsentCommand { get; }
		public void ResolveConsent(ToolApprovalResult? approval)
		{
			if (approval != null)
				ToolCall.UserConfirmationSource?.TrySetResult(approval);
		}

		/// <summary>
		/// Whether the call can be cancelled right now: it is not finished and its own cancellation source is live.
		/// A pure function of the call's state, like <see cref="InProgress"/>; the inline cancel button binds to it.
		/// </summary>
		public bool CanCancel =>
			!ToolCall.Status.IsTerminal() && ToolCall.CancellationTokenSource is { IsCancellationRequested: false };

		/// <summary>
		/// Whether the call is queued behind another one and the user can release it from the wait. A pure
		/// function of the call's state, like <see cref="CanCancel"/>.
		/// </summary>
		public bool CanSkipSynchronizationWait => ToolCall.SynchronizationWait is { IsCancellationRequested: false };

		private bool _canRestart;
		/// <summary>
		/// Whether the call can be re-run in place: it is finished and its tool still exists in the chat. Unlike
		/// <see cref="CanCancel"/> this is not a function of the call alone - the tool has to be looked up in the
		/// chat's tools - so it is refreshed when the call's status or cancellation source changes, not on every read.
		/// </summary>
		public bool CanRestart
		{
			get => _canRestart;
			private set => SetProperty(ref _canRestart, value);
		}

		/// <summary>
		/// Gets the command that cancels this call alone.
		/// </summary>
		public ICommand CancelCommand { get; }

		/// <summary>
		/// Gets the command that re-runs this finished call in place.
		/// </summary>
		public ICommand RestartCommand { get; }

		/// <summary>
		/// Gets the command that releases this call from the synchronization queue.
		/// </summary>
		public ICommand SkipSynchronizationWaitCommand { get; }

		public ToolCallViewModel(ToolCall toolCall, ChatMessage message, Chat chat)
		{
			ToolCall = toolCall;
			_message = message;
			_chat = chat;

			ToolName = toolCall.ToolName;
			ToolTitle = toolCall.Title ?? new ConstLocaleKey(toolCall.ToolName);
			ToolCallId = toolCall.ToolCallId;

			Status = toolCall.Status;
			Progress = toolCall.Progress;
			MinProgress = toolCall.MinProgress;
			MaxProgress = toolCall.MaxProgress;

			StatusIcon = toolCall.StatusIcon;
			StatusTitle = toolCall.StatusTitle;
			ExpectedBehaviour = toolCall.ExpectedBehaviour;

			AdditionalData = new AdditionalChatDataCollectionViewModel(toolCall.AdditionalData);

			ConsentCommand = new RelayCommand<ToolApprovalResult>(ResolveConsent);
			CancelCommand = new RelayCommand(Cancel);
			RestartCommand = new AsyncRelayCommand(RestartRunAsync);
			SkipSynchronizationWaitCommand = new RelayCommand(SkipSynchronizationWait);

			// The subscription is not dropped when the call finishes: a restart re-runs a finished call on the same
			// object, so a terminal status is not the end of what this card has to show.
			ToolCall.PropertyChanged += OnToolCallPropertyChanged;

			RefreshRestartAvailability();
		}

		private void OnToolCallPropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			InvokeUI(() =>
			{
				switch (e.PropertyName)
				{
					case nameof(ToolCall.Status): Status = ToolCall.Status; break;
					case nameof(ToolCall.StatusIcon): StatusIcon = ToolCall.StatusIcon; break;
					case nameof(ToolCall.StatusTitle): StatusTitle = ToolCall.StatusTitle; break;
					case nameof(ToolCall.ExpectedBehaviour): ExpectedBehaviour = ToolCall.ExpectedBehaviour; break;
					case nameof(ToolCall.Progress): Progress = ToolCall.Progress; break;
					case nameof(ToolCall.MinProgress): MinProgress = ToolCall.MinProgress; break;
					case nameof(ToolCall.MaxProgress): MaxProgress = ToolCall.MaxProgress; break;
					case nameof(ToolCall.UserConfirmationSource): RaisePropertyChanged(nameof(UserAsked)); break;
					case nameof(ToolCall.SynchronizationWait): RaisePropertyChanged(nameof(CanSkipSynchronizationWait)); break;
				}

				if (e.PropertyName is nameof(ToolCall.Status) or nameof(ToolCall.CancellationTokenSource))
				{
					RaisePropertyChanged(nameof(CanCancel));
					RefreshRestartAvailability();
				}
			});
		}

		/// <summary>
		/// Re-reads whether the call can be re-run. It is the only one of the two actions whose answer is not a
		/// property of the call: a restart needs the tool, and the tool lives in the chat's set.
		/// </summary>
		private void RefreshRestartAvailability() => CanRestart = ToolCallService.CanRestart(ToolCall);

		/// <summary>
		/// Releases this call from the synchronization queue: it runs at once without claiming any key. The
		/// ordering guarantee is dropped for this call alone, which is exactly what the user chose.
		/// </summary>
		private void SkipSynchronizationWait()
		{
			ToolCall.SynchronizationWait?.Cancel();
			RaisePropertyChanged(nameof(CanSkipSynchronizationWait));
		}

		/// <summary>
		/// Cancels this call alone. The service ignores the request for a call that is already finished, and the
		/// cancellation state is re-read at once so the button does not stay behind a request already sent.
		/// </summary>
		private void Cancel()
		{
			ToolCallService.Cancel(ToolCall);
			RaisePropertyChanged(nameof(CanCancel));
		}

		/// <summary>
		/// Re-runs the finished call in place. Nothing follows: the call is rewritten, and the next model request
		/// picks the new result up through the prompt.
		/// </summary>
		private async Task RestartRunAsync()
		{
			if (!CanRestart)
				return;

			await ToolCallService.RestartAsync(_message, ToolCall, CancellationToken.None);
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (disposing)
			{
				ToolCall.PropertyChanged -= OnToolCallPropertyChanged;

				_flyout?.Dispose();
				_flyout = null;
				AdditionalData.Dispose();
			}
		}
	}
}
