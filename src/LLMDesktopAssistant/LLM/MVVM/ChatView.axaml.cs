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
		ScrollToBottomButton.IsVisible = MessagesScrollViewer.Offset.Y < MessagesScrollViewer.Extent.Height - MessagesScrollViewer.Viewport.Height;
	}

	private void ScrollToBottomButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
	{
		SmoothScrollBehavior.AnimateTo(MessagesScrollViewer, MessagesScrollViewer.ScrollBarMaximum.Y);
	}
}
