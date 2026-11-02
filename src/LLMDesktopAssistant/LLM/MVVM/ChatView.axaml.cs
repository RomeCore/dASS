using Avalonia;
using Avalonia.Controls;

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
		bool isScrollToBottomActive = !atEnd && !MessagesScrollViewer.IsFollowingEnd;
		ScrollToBottomButton.Opacity = isScrollToBottomActive ? 1 : 0;
		ScrollToBottomButton.IsHitTestVisible = isScrollToBottomActive;
	}

	private void ScrollToBottomButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
	{
		MessagesScrollViewer.AnimateToEnd();
	}
}
