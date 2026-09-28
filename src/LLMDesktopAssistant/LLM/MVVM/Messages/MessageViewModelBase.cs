using CommunityToolkit.Mvvm.Input;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.Services;

namespace LLMDesktopAssistant.LLM.MVVM.Messages
{
	public class MessageViewModelBase : ViewModelBase
	{
		private readonly BranchedMessage branchedMessage;
		private readonly IChatOperationService chatOperator;

		/// <summary>
		/// Gets the current message being displayed.
		/// </summary>
		public ChatMessage Message => branchedMessage.Message;

		public ICommand RegenerateCommand { get; }
		public ICommand ResendCommand { get; }
		public ICommand DeleteCommand { get; }
		public ICommand SwitchBranchCommand { get; }

		public IEnumerable<int> BranchIndices =>
			Enumerable.Range(1, branchedMessage.AvailableBranchesCount);

		public int SelectedBranchIndex
		{
			get => branchedMessage.SelectedBranchIndex + 1;
			set => chatOperator.SwitchBranch(branchedMessage.MessageIndex, value - 1);
		}
		public int AvailableBranchesCount => branchedMessage.AvailableBranchesCount;
		public int PreviousBranchIndex => SelectedBranchIndex - 1;
		public int NextBranchIndex => SelectedBranchIndex + 1;
		public bool BranchSelectionAvailable => branchedMessage.AvailableBranchesCount > 1;

		public ChatViewModel ChatViewModel { get; }

		public MessageViewModelBase(BranchedMessage branchedMessage, ChatViewModel chatVM)
		{
			this.branchedMessage = branchedMessage;
			ChatViewModel = chatVM;
			chatOperator = chatVM.Chat.Services.GetRequiredService<IChatOperationService>();

			RegenerateCommand = new RelayCommand(() =>
			{
				chatOperator.RegenerateMessageAsync(branchedMessage.MessageIndex);
			});

			ResendCommand = new RelayCommand(() =>
			{
				chatOperator.ResendMessageAsync(branchedMessage.MessageIndex);
			});

			DeleteCommand = new RelayCommand(() =>
			{
				chatOperator.DeleteMessageWithDescendants(branchedMessage.MessageIndex);
			});

			SwitchBranchCommand = new RelayCommand<int>(branchIndex =>
			{
				chatOperator.SwitchBranch(branchedMessage.MessageIndex, branchIndex - 1);
			},
				branchIndex => branchIndex - 1 >= 0 && branchIndex - 1 < branchedMessage.AvailableBranchesCount);
		}
	}
}