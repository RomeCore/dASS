using System.Collections.Immutable;
using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using CommunityToolkit.Mvvm.Input;
using DocumentFormat.OpenXml.InkML;
using LLMDesktopAssistant.LLM.MVVM.Additional;
using LLMDesktopAssistant.LLM.MVVM.Messages;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.MVVM;
using LLMDesktopAssistant.Prompting;
using LLMDesktopAssistant.Services.Instances;
using Material.Icons;

namespace LLMDesktopAssistant.UIExtensions.MessageExtensions.Compaction
{
	/// <summary>
	/// The view model of the message's context compaction panel — a single segmented control
	/// that combines the five context checkpoint toggles (shield, summary, tool results,
	/// forced tool results and reasoning compaction).
	/// </summary>
	[ViewModelFor(typeof(ContextCompactionView))]
	public class ContextCompactionViewModel : ViewModelBase
	{
		private const string SummaryTooltipKey = "message.summarize_message";
		private const string SummaryInProgressTooltipKey = "message.summarization.in_progress";

		private readonly MessageViewModelBase _messageViewModel;
		private readonly AdditionalChatDataCollection _additionalData;
		private readonly IToastService _toastService;
		private readonly IChatSummarizationService _summarizationService;

		private ContextCheckpoint? _checkpoint;

		/// <summary>
		/// Gets the five toggle segments, ordered left to right.
		/// </summary>
		public ImmutableList<ContextCompactionToggleViewModel> Toggles { get; }

		public ContextCompactionViewModel(MessageViewModelBase messageViewModel)
		{
			_messageViewModel = messageViewModel;
			_additionalData = messageViewModel.Message.AdditionalData;
			_toastService = messageViewModel.ChatViewModel.Chat.Services.GetRequiredService<IToastService>();
			_summarizationService = messageViewModel.ChatViewModel.Chat.Services.GetRequiredService<IChatSummarizationService>();

			Toggles =
			[
				CreateToggle(ContextCheckpointKind.Shield, MaterialIconKind.ShieldOutline,
					"message.toggle_context_shield", new CornerRadius(8, 0, 0, 8)),

				CreateToggle(ContextCheckpointKind.Summary, MaterialIconKind.TextBoxSearchOutline,
					SummaryTooltipKey, new CornerRadius(0)),

				CreateToggle(ContextCheckpointKind.ToolCompaction, MaterialIconKind.ArchiveOutline,
					"message.toggle_tool_compaction", new CornerRadius(0)),

				CreateToggle(ContextCheckpointKind.ForcedToolCompaction, MaterialIconKind.Archive,
					"message.toggle_forced_tool_compaction", new CornerRadius(0)),

				CreateToggle(ContextCheckpointKind.ReasoningCompaction, MaterialIconKind.Brain,
					"message.toggle_reasoning_compaction", new CornerRadius(0, 8, 8, 0)),
			];

			_additionalData.CollectionChanged += OnAdditionalDataChanged;
			AttachCheckpoint(_additionalData.TryGet<ContextCheckpoint>());
			Refresh();
		}

		private ContextCompactionToggleViewModel CreateToggle(
			ContextCheckpointKind kind, VisualIconKind icon, string tooltip, CornerRadius cornerRadius)
		{
			ICommand command = kind == ContextCheckpointKind.Summary
				? new AsyncRelayCommand(ToggleSummaryAsync)
				: new RelayCommand(() => MessageExtensionHelpers.ToggleCheckpoint(_messageViewModel, kind));

			return new ContextCompactionToggleViewModel(kind, icon, tooltip, cornerRadius, command);
		}

		private async Task ToggleSummaryAsync()
		{
			if (_checkpoint != null && _checkpoint.Kind.HasFlag(ContextCheckpointKind.Summary))
			{
				_checkpoint.Context = null; // Clear the summary context if it's already set
				MessageExtensionHelpers.ToggleCheckpoint(_messageViewModel, ContextCheckpointKind.Summary);
			}
			else
			{
				var outcome = await _summarizationService.SummarizeMessageWithPreviousMessagesAsync(_messageViewModel.Message);

				switch (outcome)
				{
					case SummarizationOutcome.ModelUnavailable:
						_toastService.ShowWarning(
							Locale.Get("message.summarization.unavailable.title"),
							Locale.Get("message.summarization.unavailable.description"));
						break;

					case SummarizationOutcome.Failed:
						_toastService.ShowError(
							Locale.Get("message.summarization.failed.title"),
							Locale.Get("message.summarization.failed.description"));
						break;
				}
			}
		}

		private void OnAdditionalDataChanged(object? sender, NotifyCollectionChangedEventArgs e)
		{
			// The checkpoint may be added or removed as a whole; rebind to it and refresh.
			AttachCheckpoint(_additionalData.TryGet<ContextCheckpoint>());
			Refresh();
		}

		private void AttachCheckpoint(ContextCheckpoint? checkpoint)
		{
			if (ReferenceEquals(_checkpoint, checkpoint))
				return;

			if (_checkpoint != null)
				_checkpoint.PropertyChanged -= OnCheckpointPropertyChanged;

			_checkpoint = checkpoint;

			if (_checkpoint != null)
				_checkpoint.PropertyChanged += OnCheckpointPropertyChanged;
		}

		private void OnCheckpointPropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName is nameof(ContextCheckpoint.Kind) or nameof(ContextCheckpoint.IsCompletedAndEnabled))
				Refresh();
		}

		private void Refresh()
		{
			var checkpoint = _checkpoint;
			var kind = checkpoint?.Kind ?? ContextCheckpointKind.None;
			bool summaryBusy = kind.HasFlag(ContextCheckpointKind.Summary)
				&& checkpoint?.IsCompletedAndEnabled == false;

			foreach (var toggle in Toggles)
			{
				if (toggle.Kind == ContextCheckpointKind.Summary)
				{
					toggle.IsEnabled = !summaryBusy;
					toggle.IsChecked = summaryBusy ? null : kind.HasFlag(ContextCheckpointKind.Summary);
					toggle.Tooltip = summaryBusy ? SummaryInProgressTooltipKey : SummaryTooltipKey;
				}
				else
				{
					toggle.IsChecked = kind.HasFlag(toggle.Kind);
				}
			}
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (disposing)
			{
				_additionalData.CollectionChanged -= OnAdditionalDataChanged;
				AttachCheckpoint(null);
			}
		}
	}
}
