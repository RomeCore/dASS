using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;

namespace LLMDesktopAssistant.Controls.Text;

/// <summary>
/// TextBox with support for text range highlighting and text transformation (for ghost text).
/// Behavior, input, caret, selection and scrolling are native - only rendering is customized.
/// </summary>
/// <remarks>
/// Requires a theme with <see cref="HighlightTextPresenter"/> in PART_TextPresenter
/// (see HighlightTextBoxTheme); otherwise it works as a plain TextBox without highlighting.
/// </remarks>
public class HighlightTextBox : TextBox
{
	private HighlightTextPresenter? _presenter;

	/// <summary>
	/// Defines the <see cref="HighlightTransformProvider"/> property.
	/// </summary>
	public static readonly StyledProperty<IHighlightTransformProvider?> HighlightTransformProviderProperty =
		AvaloniaProperty.Register<HighlightTextBox, IHighlightTransformProvider?>(nameof(HighlightTransformProvider));

	/// <summary>
	/// The provider that computes the rendered text and/or highlight spans for the current text.
	/// The provider is invoked on every text change; raise <see cref="IHighlightTransformProvider.LayoutChanged"/>
	/// to recompute the layout when the provider state changed without a text change.
	/// </summary>
	public IHighlightTransformProvider? HighlightTransformProvider
	{
		get => GetValue(HighlightTransformProviderProperty);
		set => SetValue(HighlightTransformProviderProperty, value);
	}

	static HighlightTextBox()
	{
		AffectsArrange<HighlightTextBox>(HighlightTransformProviderProperty);
		AffectsMeasure<HighlightTextBox>(HighlightTransformProviderProperty);
		AffectsRender<HighlightTextBox>(HighlightTransformProviderProperty);

		HighlightTransformProviderProperty.Changed.AddClassHandler<HighlightTextBox>((s, e) =>
		{
			s._presenter?.HighlightTransformProvider = s.HighlightTransformProvider;
		});
	}

	protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
	{
		base.OnApplyTemplate(e);

		_presenter = e.NameScope.Find<HighlightTextPresenter>("PART_TextPresenter");
		if (_presenter != null)
			_presenter.HighlightTransformProvider = HighlightTransformProvider;
	}

	public void InvalidateTextLayout()
	{
		_presenter?.InvalidateTextLayoutPublic();
	}

	// Clicks and drag-selection must not move the caret/selection into the ghost zone (beyond Text.Length).
	protected override void OnPointerPressed(PointerPressedEventArgs e)
	{
		base.OnPointerPressed(e);
		ClampCaretAndSelectionToText();
	}

	protected override void OnPointerMoved(PointerEventArgs e)
	{
		base.OnPointerMoved(e);
		ClampCaretAndSelectionToText();
	}

	private void ClampCaretAndSelectionToText()
	{
		if (_presenter is not { } presenter)
			return;

		var length = Text?.Length ?? 0;
		if (presenter.CaretIndex > length)
			presenter.CaretIndex = length;
		if (presenter.SelectionStart > length)
			presenter.SelectionStart = length;
		if (presenter.SelectionEnd > length)
			presenter.SelectionEnd = length;
	}
}
