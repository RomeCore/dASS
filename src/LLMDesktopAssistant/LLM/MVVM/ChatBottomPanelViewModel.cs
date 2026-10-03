namespace LLMDesktopAssistant.LLM.MVVM
{
	/// <summary>
	/// The bottom panel of the chat: the user input on top, with the left and right
	/// toolbars below it.
	/// </summary>
	[ViewModelFor(typeof(ChatBottomPanelView))]
	public class ChatBottomPanelViewModel : ViewModelBase
	{
		/// <summary>
		/// Gets the user input view model.
		/// </summary>
		public UserInputViewModel UserInput { get; }

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
			LeftToolBar = new ChatLeftToolBarViewModel(UserInput);
			RightToolBar = new ChatRightToolBarViewModel(UserInput);
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (disposing)
			{
				LeftToolBar.Dispose();
				RightToolBar.Dispose();
				UserInput.Dispose();
			}
		}
	}
}
