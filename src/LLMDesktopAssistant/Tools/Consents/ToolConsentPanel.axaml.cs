using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Tools.Execution.Domain;

namespace LLMDesktopAssistant.Tools.Consents;

/// <summary>
/// A reusable panel for resolving tool consent: approve/reject buttons, an editable note
/// combo box with presets (for example "wait" or "try another tool") and a "remember"
/// combo box. Executes <see cref="ResolveConsentCommand"/> with the resulting
/// <see cref="ToolApprovalResult"/> when the user approves or rejects the tool call.
/// </summary>
public partial class ToolConsentPanel : UserControl
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ToolConsentPanel"/> class.
	/// </summary>
	public ToolConsentPanel()
	{
		InitializeComponent();

		NoteComboBox.ItemsSource = new[]
		{
			LocalizationManager.LocalizeStatic("tool.call.note.wait"),
			LocalizationManager.LocalizeStatic("tool.call.note.try_another_tool")
		};

		MemorizationComboBox.ItemsSource = ToolMemorizationOptions.Create();
		MemorizationComboBox.SelectedIndex = 0;
	}

	/// <summary>
	/// Identifies the <see cref="ResolveConsentCommand"/> dependency property.
	/// </summary>
	public static readonly StyledProperty<ICommand?> ResolveConsentCommandProperty =
		AvaloniaProperty.Register<ToolConsentPanel, ICommand?>(nameof(ResolveConsentCommand));

	/// <summary>
	/// Gets or sets the command that is executed with the resulting <see cref="ToolApprovalResult"/>
	/// when the user approves or rejects the tool call.
	/// </summary>
	public ICommand? ResolveConsentCommand
	{
		get => GetValue(ResolveConsentCommandProperty);
		set => SetValue(ResolveConsentCommandProperty, value);
	}

	/// <summary>
	/// Identifies the <see cref="ShowNoteOptions"/> dependency property.
	/// </summary>
	public static readonly StyledProperty<bool> ShowNoteOptionsProperty =
		AvaloniaProperty.Register<ToolConsentPanel, bool>(nameof(ShowNoteOptions), defaultValue: true);

	/// <summary>
	/// Gets or sets a value indicating whether the note combo box is shown.
	/// Set to <see langword="false"/> when the panel is used without note/memorization
	/// options (for example, for self-handled confirmations).
	/// </summary>
	public bool ShowNoteOptions
	{
		get => GetValue(ShowNoteOptionsProperty);
		set => SetValue(ShowNoteOptionsProperty, value);
	}

	/// <summary>
	/// Identifies the <see cref="ShowMemorization"/> dependency property.
	/// </summary>
	public static readonly StyledProperty<bool> ShowMemorizationProperty =
		AvaloniaProperty.Register<ToolConsentPanel, bool>(nameof(ShowMemorization), defaultValue: true);

	/// <summary>
	/// Gets or sets a value indicating whether the memorization combo box is shown.
	/// Set to <see langword="false"/> when the panel is used without note/memorization
	/// options (for example, for self-handled confirmations).
	/// </summary>
	public bool ShowMemorization
	{
		get => GetValue(ShowMemorizationProperty);
		set => SetValue(ShowMemorizationProperty, value);
	}

	private void RejectButton_Click(object? sender, RoutedEventArgs e) => Resolve(approved: false);

	private void ApproveButton_Click(object? sender, RoutedEventArgs e) => Resolve(approved: true);

	private void Resolve(bool approved)
	{
		var note = NoteComboBox.Text?.Trim();

		var result = new ToolApprovalResult
		{
			Approved = approved,
			Memorization = (MemorizationComboBox.SelectedItem as ToolMemorizationOption)?.Memorization
				?? ToolApprovalMemorization.Once,
			Notes = string.IsNullOrWhiteSpace(note) ? null : note
		};

		ResolveConsentCommand?.Execute(result);
	}
}
