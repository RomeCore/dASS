using CommunityToolkit.Mvvm.Input;
using LLMDesktopAssistant.Controls.Dialogs;
using LLMDesktopAssistant.MVVM;

namespace LLMDesktopAssistant.LLM.MVVM
{
	/// <summary>
	/// The values a chat edit dialog returns.
	/// </summary>
	public readonly record struct ChatEditResult(string Title, string Topic, string SubTopic);

	/// <summary>
	/// Edits a chat's title, topic and sub-topic in one dialog.
	/// </summary>
	[ViewModelFor(typeof(EditChatDialogView))]
	public class EditChatDialogViewModel : NotifyPropertyChanged
	{
		private bool _isResultSet;

		/// <summary>
		/// Gets or sets the chat title. A chat is never saved with an empty one.
		/// </summary>
		public string? ChatTitle
		{
			get => field;
			set => SetProperty(ref field, value);
		}

		/// <summary>
		/// Gets or sets the chat topic: the broad category the chat belongs to.
		/// </summary>
		public string? ChatTopic
		{
			get => field;
			set
			{
				if (SetProperty(ref field, value))
					RaisePropertyChanged(nameof(ChatTopicColor));
			}
		}
		public string ChatTopicColor => ChatTopicHashColor.GetHex(ChatTopic);

		/// <summary>
		/// Gets or sets the sub-topic: the narrower label inside the topic.
		/// </summary>
		public string? ChatSubTopic
		{
			get => field;
			set
			{
				if (SetProperty(ref field, value))
					RaisePropertyChanged(nameof(ChatSubTopicColor));
			}
		}
		public string ChatSubTopicColor => ChatTopicHashColor.GetHex(ChatSubTopic);

		/// <summary>
		/// Gets the command that applies the edited values.
		/// </summary>
		public IRelayCommand SubmitCommand { get; }

		/// <summary>
		/// Gets the command that discards them.
		/// </summary>
		public IRelayCommand CancelCommand { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="EditChatDialogViewModel"/> class.
		/// </summary>
		public EditChatDialogViewModel()
		{
			SubmitCommand = new RelayCommand(Submit);
			CancelCommand = new RelayCommand(Cancel);
		}

		private void Submit()
		{
			if (_isResultSet || string.IsNullOrWhiteSpace(ChatTitle))
				return;

			_isResultSet = true;
			DialogManager.CloseDialog(new ChatEditResult(
				ChatTitle!.Trim(),
				ChatTopic?.Trim() ?? string.Empty,
				ChatSubTopic?.Trim() ?? string.Empty));
		}

		private void Cancel()
		{
			if (_isResultSet)
				return;

			_isResultSet = true;
			DialogManager.CloseDialog(null);
		}
	}
}
