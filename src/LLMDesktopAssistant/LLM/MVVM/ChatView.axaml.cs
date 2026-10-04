using Avalonia;
using Avalonia.Controls;
using LLMDesktopAssistant.Controls.Behaviours;

namespace LLMDesktopAssistant.LLM.MVVM;

public partial class ChatView : UserControl
{
	public ChatView()
	{
		InitializeComponent();
	}

	private void MessagesScrollViewer_ScrollChanged(object? sender, ScrollChangedEventArgs e)
	{
		var atEnd = MessagesScrollViewer.Offset.Y >= MessagesScrollViewer.ScrollBarMaximum.Y - 1d;

		// While the end of the streamed content is being followed the offset intentionally lags
		// a bit behind the maximum, so don't flash the button during that chase.
		ScrollToBottomButton.IsVisible =
			!atEnd && !SmoothScrollBehavior.GetIsFollowingEnd(MessagesScrollViewer);
	}

	private void ScrollToBottomButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
	{
		SmoothScrollBehavior.AnimateTo(MessagesScrollViewer, MessagesScrollViewer.ScrollBarMaximum.Y);
	}
}
