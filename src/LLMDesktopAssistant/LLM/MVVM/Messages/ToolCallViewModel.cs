using System.ComponentModel;
using Avalonia.Input.Platform;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.MVVM.Settings;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Tools;
using LLMDesktopAssistant.Tools.Consents;
using LLMDesktopAssistant.Utils;
using Material.Icons;

namespace LLMDesktopAssistant.LLM.MVVM.Messages
{
	[ViewModelFor(typeof(ToolCallView))]
	public class ToolCallViewModel : ViewModelBase
	{
		private readonly ToolCall toolCall;

		public string ToolName { get; }

		public LocaleKeyBase ToolTitle { get; }

		public string ToolCallId { get; }

		private string _arguments = string.Empty;
		public string Arguments
		{
			get => _arguments;
			set => SetProperty(ref _arguments, value);
		}



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

		private MaterialIconKind? _statusIcon;
		public MaterialIconKind? StatusIcon
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

		private string? _result = string.Empty;
		public string? Result
		{
			get => _result;
			set => SetProperty(ref _result, value);
		}

		private bool _useMarkdown = false;
		public bool UseMarkdown
		{
			get => _useMarkdown;
			set => SetProperty(ref _useMarkdown, value);
		}
		


		public MaterialIconKind ToolIcon =>
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
				ToolStatus.WaitingForApproval => true,
				_ => false
			};

		/// <summary>
		/// Gets the command that resolves the tool confirmation with a <see cref="ToolConsentResult"/>.
		/// Executed by the <see cref="ToolConsentPanel"/>.
		/// </summary>
		public ICommand ConsentCommand { get; }
		public void ResolveConsent(ToolConsentResult? consentResult)
		{
			if (consentResult != null)
				toolCall.UserConfirmationSource?.TrySetResult(consentResult);
		}

		public ICommand CopyArgumentsCommand { get; }
		public void CopyArguments()
		{
			if (!string.IsNullOrEmpty(toolCall.Arguments))
			{
				App.MainTopLevel.Clipboard?.SetTextAsync(toolCall.Arguments);
			}
		}

		public ICommand CopyResultCommand { get; }
		public void CopyResult()
		{
			if (!string.IsNullOrEmpty(Result))
			{
				App.MainTopLevel.Clipboard?.SetTextAsync(Result);
			}
		}



		public ToolCallViewModel(ToolCall toolCall, Chat chat)
		{
			this.toolCall = toolCall;

			ToolName = toolCall.ToolName;
			ToolTitle = toolCall.Title ?? new ConstLocaleKey(toolCall.ToolName);
			ToolCallId = toolCall.ToolCallId;
			try
			{
				var parsedArgs = TolerantJsonParser.Parse(toolCall.Arguments);
				Arguments = ToolCallArgumentFormatter.FormatToMarkdown(parsedArgs);
			}
			catch
			{
				Arguments = "```json\n" + toolCall.Arguments + "\n```";
			}

			Status = toolCall.Status;
			Progress = toolCall.ReactiveToolResult?.Progress;
			MinProgress = toolCall.ReactiveToolResult?.MinProgress ?? 0.0;
			MaxProgress = toolCall.ReactiveToolResult?.MaxProgress ?? 1.0;

			StatusIcon = toolCall.StatusIcon;
			StatusTitle = toolCall.StatusTitle;
			ExpectedBehaviour = toolCall.ExpectedBehaviour;

			Result = toolCall.ResultContent;
			UseMarkdown = toolCall.UseMarkdown;

			ConsentCommand = new RelayCommand<ToolConsentResult>(ResolveConsent);
			CopyArgumentsCommand = new RelayCommand(CopyArguments);
			CopyResultCommand = new RelayCommand(CopyResult);

			if (!toolCall.IsCompleted)
			{
				var reactiveToolResult = toolCall.ReactiveToolResult;
				void OnToolCallPropertyChanged(object? s, PropertyChangedEventArgs e)
				{
					InvokeUI(() =>
					{
						switch (e.PropertyName)
						{
							case nameof(ToolCall.Arguments):
								try
								{
									var parsedArgs = TolerantJsonParser.Parse(toolCall.Arguments);
									Arguments = ToolCallArgumentFormatter.FormatToMarkdown(parsedArgs);
								}
								catch
								{
									Arguments = "```json\n" + toolCall.Arguments + "\n```";
								}
								break;

							case nameof(ToolCall.Status): Status = toolCall.Status; break;
							case nameof(ToolCall.StatusIcon): StatusIcon = toolCall.StatusIcon; break;
							case nameof(ToolCall.StatusTitle): StatusTitle = toolCall.StatusTitle; break;
							case nameof(ToolCall.ExpectedBehaviour): ExpectedBehaviour = toolCall.ExpectedBehaviour; break;
							case nameof(ToolCall.ResultContent): Result = toolCall.ResultContent; break;
							case nameof(ToolCall.UseMarkdown): UseMarkdown = toolCall.UseMarkdown; break;

							case nameof(ToolCall.ReactiveToolResult):
								reactiveToolResult?.PropertyChanged -= OnReactiveToolResultPropertyChanged;
								reactiveToolResult = toolCall.ReactiveToolResult;
								reactiveToolResult?.PropertyChanged += OnReactiveToolResultPropertyChanged;
								OnReactiveToolResultPropertyChanged(null, null!);
								break;
						}
					});
				}
				void OnReactiveToolResultPropertyChanged(object? s, PropertyChangedEventArgs e)
				{
					InvokeUI(() =>
					{
						Progress = reactiveToolResult?.Progress;
						MinProgress = reactiveToolResult?.MinProgress ?? 0.0;
						MaxProgress = reactiveToolResult?.MaxProgress ?? 1.0;
					});
				}

				toolCall.PropertyChanged += OnToolCallPropertyChanged;
				reactiveToolResult?.PropertyChanged += OnReactiveToolResultPropertyChanged;
				toolCall.CompletionToken.OnCompleted(() =>
				{
					toolCall.PropertyChanged -= OnToolCallPropertyChanged;
					reactiveToolResult?.PropertyChanged -= OnReactiveToolResultPropertyChanged;
				});
			}
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (disposing)
			{
				
			}
		}
	}
}
