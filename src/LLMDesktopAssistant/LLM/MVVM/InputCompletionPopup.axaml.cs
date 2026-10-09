using System;
using Avalonia.Controls;
using Avalonia.Input;

namespace LLMDesktopAssistant.LLM.MVVM;

/// <summary>
/// The caret-anchored completion list shown over the input. It is a passive list: the text box keeps focus, and a
/// click on a row raises <see cref="AcceptRequested"/> for the owning view to apply.
/// </summary>
public partial class InputCompletionPopup : UserControl
{
	/// <summary>Raised when a row was clicked.</summary>
	public event EventHandler? AcceptRequested;

	public InputCompletionPopup()
	{
		InitializeComponent();
		AddHandler(PointerReleasedEvent, OnPointerReleased);
	}

	private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
	{
		if (e.InitialPressMouseButton == MouseButton.Left)
			AcceptRequested?.Invoke(this, EventArgs.Empty);
	}
}
