using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;

namespace LLMDesktopAssistant.Controls.Text;

/// <summary>
/// TextBox with support for text range highlighting, text transformation (for ghost text) and inline completion.
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

	/// <summary>
	/// Raised after the caret or the selection changed.
	/// </summary>
	public event EventHandler? CaretStateChanged;

	/// <summary>
	/// Raised after a pointer-driven caret/selection change (a click or a drag-select). The owning view can use it to
	/// reset a completion, because the caret was moved by hand.
	/// </summary>
	public event EventHandler? PointerCaretStateChanged;

	/// <summary>
	/// Raised at the start of <see cref="OnKeyDown"/>, before the base behaviour. Set
	/// <see cref="KeyEventArgs.Handled"/> to consume the key.
	/// </summary>
	public event EventHandler<KeyEventArgs>? PreviewKeyDown;

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

		if (_presenter != null)
			_presenter.PropertyChanged -= OnPresenterPropertyChanged;

		_presenter = e.NameScope.Find<HighlightTextPresenter>("PART_TextPresenter");
		if (_presenter != null)
		{
			_presenter.HighlightTransformProvider = HighlightTransformProvider;
			_presenter.PropertyChanged += OnPresenterPropertyChanged;
		}
	}

	/// <summary>
	/// Returns the caret rectangle in the coordinates of the specified visual, or <see langword="null"/> when the
	/// presenter is not ready.
	/// </summary>
	public Rect? GetCaretRect(Visual target) => _presenter?.GetCaretRectIn(target);

	private void OnPresenterPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
	{
		if (e.Property == TextPresenter.CaretIndexProperty
			|| e.Property == TextPresenter.SelectionStartProperty
			|| e.Property == TextPresenter.SelectionEndProperty)
		{
			CaretStateChanged?.Invoke(this, EventArgs.Empty);
		}
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
		PointerCaretStateChanged?.Invoke(this, EventArgs.Empty);
	}

	protected override void OnPointerMoved(PointerEventArgs e)
	{
		base.OnPointerMoved(e);
		ClampCaretAndSelectionToText();

		if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
			PointerCaretStateChanged?.Invoke(this, EventArgs.Empty);
	}

	protected override void OnKeyDown(KeyEventArgs e)
	{
		PreviewKeyDown?.Invoke(this, e);
		if (e.Handled)
			return;

		if (e.Key == Key.Right && TryAcceptInlineCompletion())
		{
			e.Handled = true;
			return;
		}

		base.OnKeyDown(e);
	}

	private bool TryAcceptInlineCompletion()
	{
		if (HighlightTransformProvider is not IInlineCompletionProvider provider || _presenter is not { } presenter)
			return false;

		var completion = provider.CompletionText;
		if (string.IsNullOrEmpty(completion))
			return false;

		if (InlineCompletionAcceptor.Accept(Text, presenter.CaretIndex, completion, provider.TokenEnd)
			is not { } accepted)
			return false;

		Text = accepted.Text;
		CaretIndex = accepted.Caret;
		presenter.CaretIndex = accepted.Caret;
		return true;
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
