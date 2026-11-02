using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace LLMDesktopAssistant.LLM.MVVM.Messages;

public partial class MessageSequenceView : UserControl
{
    private MessageSequenceViewModel? _subscribed;

    public MessageSequenceView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_subscribed != null)
            _subscribed.BringIntoViewRequested -= OnBringIntoViewRequested;

        _subscribed = DataContext as MessageSequenceViewModel;

        if (_subscribed != null)
            _subscribed.BringIntoViewRequested += OnBringIntoViewRequested;
    }

    /// <summary>
    /// Scrolls the container of the requested message into view. The items control is not virtualized (message height
    /// varies, virtualization made scrolling worse), so the container is already realized.
    /// </summary>
    private void OnBringIntoViewRequested(MessageViewModelBase viewModel)
    {
        var container = this.GetVisualDescendants()
            .OfType<Control>()
            .FirstOrDefault(control => ReferenceEquals(control.DataContext, viewModel));

        container?.BringIntoView();
    }
}
