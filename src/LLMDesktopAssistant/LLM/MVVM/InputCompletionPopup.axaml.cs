using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace LLMDesktopAssistant.LLM.MVVM;

/// <summary>
/// The caret-anchored completion list shown over the input. It is a passive stack: the text box keeps focus, and a
/// click on a picker row selects that row and raises <see cref="AcceptRequested"/> for the owning view to apply.
/// </summary>
public partial class InputCompletionPopup : UserControl
{
	/// <summary>Raised when a picker row was clicked.</summary>
	public event EventHandler? AcceptRequested;

	public InputCompletionPopup()
	{
		InitializeComponent();

		// Only the picker accepts: a click on the state or the context block does nothing.
		PickerList.AddHandler(PointerReleasedEvent, OnPointerReleased);
	}

	private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
	{
		if (e.InitialPressMouseButton != MouseButton.Left)
			return;

		// The click selects the row it landed on, so an accept always applies the row the user pointed at.
		if (DataContext is InputCompletionViewModel viewModel && RowOf(e.Source) is { } row)
			viewModel.SelectedIndex = IndexOf(viewModel.Rows, row);

		AcceptRequested?.Invoke(this, EventArgs.Empty);
	}

	private static InputCompletionRow? RowOf(object? source)
	{
		for (var visual = source as Visual; visual is not null; visual = visual.GetVisualParent())
		{
			if (visual is Control { DataContext: InputCompletionRow row })
				return row;
		}

		return null;
	}

	private static int IndexOf(IReadOnlyList<InputCompletionRow> rows, InputCompletionRow row)
	{
		for (var i = 0; i < rows.Count; i++)
		{
			if (ReferenceEquals(rows[i], row))
				return i;
		}

		return 0;
	}
}
