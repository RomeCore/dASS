using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Serilog;

namespace LLMDesktopAssistant.LLM.MVVM;

public partial class UserInputView : UserControl
{
	public UserInputView()
	{
		InitializeComponent();

		InputTextBox.PastingFromClipboard += InputTextBox_PastingFromClipboard;
		InputTextBox.AddHandler(TextBox.KeyDownEvent, InputTextBox_KeyDown, RoutingStrategies.Tunnel);
		InputTextBox.CaretStateChanged += (_, _) => OnCaretChanged();
		InputTextBox.PointerCaretStateChanged += (_, _) => (DataContext as UserInputViewModel)?.Completion.Close();
		CompletionList.AcceptRequested += (_, _) => AcceptCompletion();

		DragDrop.SetAllowDrop(this, true);
		AddHandler(DragDrop.DragEnterEvent, OnDragEnter);
		AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
		AddHandler(DragDrop.DropEvent, OnDrop);
	}

	private void OnCaretChanged()
	{
		if (DataContext is not UserInputViewModel viewModel)
			return;

		viewModel.OnCompletionCaretChanged(InputTextBox.CurrentCaretPosition);

		if (CompletionPopup.IsOpen && InputTextBox.GetCaretRect(InputTextBox) is { } rect)
		{
			CompletionPopup.HorizontalOffset = rect.X;
			CompletionPopup.VerticalOffset = rect.Bottom;
		}
	}

	private void AcceptCompletion()
	{
		if (DataContext is not UserInputViewModel viewModel || viewModel.Completion.Accept() is not { } accepted)
			return;

		InputTextBox.Text = accepted.Text;
		InputTextBox.SetCaretPosition(accepted.Caret);
		viewModel.Completion.Close();
	}

	private async void InputTextBox_PastingFromClipboard(object? sender, RoutedEventArgs e)
	{
		if (DataContext is not UserInputViewModel vm)
			return;

		var clipboard = App.MainTopLevel.Clipboard;
		if (clipboard == null)
			return;

		try
		{
			var image = await clipboard.TryGetBitmapAsync();
			if (image != null)
			{
				_ = vm.AcceptImageAsync(image);
				e.Handled = true;
				return;
			}

			var files = await clipboard.TryGetFilesAsync();
			if (files != null && files.Length > 0)
			{
				_ = vm.AcceptFilesAsync(files);
				e.Handled = true;
				return;
			}
		}
		catch (Exception ex)
		{
			Log.Warning(ex, "Failed to paste from clipboard: {Error}", ex.Message);
		}
	}

	private void InputTextBox_KeyDown(object? sender, KeyEventArgs e)
	{
		if (DataContext is not UserInputViewModel viewModel)
			return;

		if (e.KeyModifiers != KeyModifiers.None)
			return;

		// Enter accepts the completion when the popup is open; otherwise it sends.
		if (e.Key is Key.Enter or Key.Tab && viewModel.Completion.CanAccept)
		{
			AcceptCompletion();
			e.Handled = true;
			return;
		}

		if (e.Key == Key.Enter)
		{
			if (viewModel.IsGenerating)
				viewModel.CancelGenerationCommand.Execute(null);
			else if (!viewModel.IsEmpty)
				viewModel.SendCurrentUserInputAsync(generate: true);
			e.Handled = true;
			return;
		}

		// Up/Down/Escape are the popup's keys; Right is handled by the control (inline completion).
		if (viewModel.Completion.TryHandleKey(e.Key))
			e.Handled = true;
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

		if (DataContext is UserInputViewModel vm)
		{
			_ = vm.AcceptDropAsync(e);
		}

		e.Handled = true;
	}
}
