using LLMDesktopAssistant.LLM.MVVM.Additional.Chat;

namespace LLMDesktopAssistant.LLM.MVVM
{
	/// <summary>
	/// The bottom panel of the chat: the two input rail strips, the user input between them, and the left
	/// and right toolbars below.
	/// </summary>
	[ViewModelFor(typeof(ChatBottomPanelView))]
	public class ChatBottomPanelViewModel : ViewModelBase
	{
		/// <summary>
		/// Gets the rail strip above the input.
		/// </summary>
		public ChatInputRailViewModel TopRail { get; }

		/// <summary>
		/// Gets the user input view model.
		/// </summary>
		public UserInputViewModel UserInput { get; }

		/// <summary>
		/// Gets the rail strip below the input, above the native toolbars.
		/// </summary>
		public ChatInputRailViewModel BottomRail { get; }

		/// <summary>
		/// Gets the left toolbar (configuration buttons).
		/// </summary>
		public ChatLeftToolBarViewModel LeftToolBar { get; }

		/// <summary>
		/// Gets the right toolbar (sender/visibility/send controls).
		/// </summary>
		public ChatRightToolBarViewModel RightToolBar { get; }

		public ChatBottomPanelViewModel(ChatViewModel chatVM)
		{
			UserInput = new UserInputViewModel(chatVM);
			TopRail = new ChatInputRailViewModel(chatVM.Chat.AdditionalData, ChatInputRailVerticalPlacement.Top);
			BottomRail = new ChatInputRailViewModel(chatVM.Chat.AdditionalData, ChatInputRailVerticalPlacement.Bottom);
			LeftToolBar = new ChatLeftToolBarViewModel(UserInput);
			RightToolBar = new ChatRightToolBarViewModel(UserInput);
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (disposing)
			{
				TopRail.Dispose();
				BottomRail.Dispose();
				LeftToolBar.Dispose();
				RightToolBar.Dispose();
				UserInput.Dispose();
			}
		}
	}
}
