using AngleSharp.Dom;
using Avalonia.Controls;
using Avalonia.Input;

namespace LLMDesktopAssistant.LLM.MVVM.Attachments;

public partial class AttachmentsManagerView : UserControl
{
	public AttachmentsManagerView()
	{
		InitializeComponent();

		DragDrop.SetAllowDrop(this, true);
		AddHandler(DragDrop.DragEnterEvent, OnDragEnter);
		AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
		AddHandler(DragDrop.DropEvent, OnDrop);
	}

	private void OnUrlInputKeyDown(object? sender, KeyEventArgs e)
	{
		if (e.Key == Key.Enter && DataContext is AttachmentsManagerViewModel vm)
		{
			vm.AddUrlCommand.Execute(null);
			e.Handled = true;
		}
	}

	private void OnDragEnter(object? sender, DragEventArgs e)
	{
		if (e.DataTransfer.Contains(DataFormat.File) ||
			e.DataTransfer.Contains(DataFormat.Text))
		{
			e.DragEffects = DragDropEffects.Copy;
			DropOverlay.IsVisible = true;
		}
		else
		{
			e.DragEffects = DragDropEffects.None;
		}

		e.Handled = true;
	}

	private void OnDragLeave(object? sender, DragEventArgs e)
	{
		DropOverlay.IsVisible = false;
	}

	private void OnDrop(object? sender, DragEventArgs e)
	{
		DropOverlay.IsVisible = false;

		if (DataContext is AttachmentsManagerViewModel vm)
		{
			vm.AcceptDrop(e);
		}

		e.Handled = true;
	}
}