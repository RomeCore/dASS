using Avalonia;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Utilities;

namespace LLMDesktopAssistant.Controls.Text;

/// <summary>
/// TextPresenter that can color individual text ranges (slash commands, mentions etc.),
/// show ghost text (autocomplete preview) and transform the string before rendering.
/// </summary>
/// <remarks>
/// A transform may replace the text from the caret onwards — the completion ghost substitutes the token's tail
/// instead of being appended — so the rendered string and the real one differ in length, and the caret is mapped
/// against the layout's own text. That is safe only because the caret and the selection are clamped to the real text
/// and the owning view resets the completion whenever the pointer moves them, so a stale region never outlives the
/// caret it was computed for.
/// </remarks>
public class HighlightTextPresenter : TextPresenter
{
	/// <summary>
	/// Defines the <see cref="HighlightTransformProvider"/> property.
	/// </summary>
	public static readonly StyledProperty<IHighlightTransformProvider?> HighlightTransformProviderProperty =
		AvaloniaProperty.Register<HighlightTextPresenter, IHighlightTransformProvider?>(
			nameof(HighlightTransformProvider));

	/// <summary>
	/// Gets or sets the provider that computes the rendered text and/or highlight spans.
	/// </summary>
	public IHighlightTransformProvider? HighlightTransformProvider
	{
		get => GetValue(HighlightTransformProviderProperty);
		set => SetValue(HighlightTransformProviderProperty, value);
	}

	private readonly TextRunCache _runCache = new();
	private Size _constraint;

	/// <summary>
	/// Returns the caret rectangle in coordinates of the specified visual.
	/// </summary>
	public Rect GetCaretRectIn(Visual target)
	{
		var rect = TextLayout.HitTestTextPosition(CaretIndex);
		if (!ReferenceEquals(target, this))
		{
			var offset = this.TranslatePoint(new Point(0, 0), target);
			if (offset.HasValue)
				rect = rect.Translate(offset.Value);
		}
		return rect;
	}

	protected override Size MeasureOverride(Size availableSize)
	{
		// The base class keeps the constraint in a private field - duplicate it for layout building.
		_constraint = availableSize;
		return base.MeasureOverride(availableSize);
	}

	/// <inheritdoc/>
	protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
	{
		base.OnPropertyChanged(change);

		if (change.Property == HighlightTransformProviderProperty)
		{
			SubscribeToProvider((IHighlightTransformProvider?)change.OldValue,
				(IHighlightTransformProvider?)change.NewValue);
			InvalidateTextLayout();
		}
	}

	private void SubscribeToProvider(IHighlightTransformProvider? oldProvider,
		IHighlightTransformProvider? newProvider)
	{
		if (ReferenceEquals(oldProvider, newProvider))
			return;

		oldProvider?.LayoutChanged -= OnProviderLayoutChanged;
		newProvider?.LayoutChanged += OnProviderLayoutChanged;
	}

	private void OnProviderLayoutChanged(object? sender, EventArgs e) => InvalidateTextLayout();

	/// <summary>
	/// Invalidates the text layout and forces a re-layout of the text. This is a public version of the protected method.
	/// </summary>
	public void InvalidateTextLayoutPublic()
	{
		InvalidateTextLayout();
	}

	protected override void InvalidateTextLayout()
	{
		// IMPORTANT: the base class invalidates only its own private TextRunCache (which stays
		// null here because we build the layout ourselves), so our own cache must be invalidated
		// too. TextRunCache is keyed by the text source index, NOT by the text content: without
		// this, a rebuilt TextLayout returns stale shaped runs and text edits never get rendered.
		_runCache.Invalidate();
		base.InvalidateTextLayout();
	}

	protected override TextLayout CreateTextLayout()
	{
		// Text + IME composition (a copy of the base class private logic)
		var caretIndex = CaretIndex;
		var preeditText = PreeditText;
		var text = Text ?? string.Empty;
		if (!string.IsNullOrEmpty(preeditText))
			text = text.Insert(Math.Min(caretIndex, text.Length), preeditText);

		var typeface = new Typeface(FontFamily, FontStyle, FontWeight, FontStretch);
		var overrides = new List<ValueSpan<TextRunProperties>>();

		// Base class standard overrides: preedit underline and selected text color
		if (!string.IsNullOrEmpty(preeditText))
		{
			overrides.Add(new ValueSpan<TextRunProperties>(caretIndex, preeditText.Length,
				new GenericTextRunProperties(typeface, FontSize, TextDecorations.Underline, Foreground,
					fontFeatures: FontFeatures)));
		}
		else if (ShowSelectionHighlight && SelectionForegroundBrush is { } selectionBrush)
		{
			var start = Math.Min(SelectionStart, SelectionEnd);
			var length = Math.Max(SelectionStart, SelectionEnd) - start;
			if (length > 0)
			{
				overrides.Add(new ValueSpan<TextRunProperties>(start, length,
					new GenericTextRunProperties(typeface, FontSize, foregroundBrush: selectionBrush,
						fontFeatures: FontFeatures)));
			}
		}

		// User transform provider: highlight spans first (raw text coordinates), then rendered text
		if (HighlightTransformProvider is { } provider)
		{
			var result = provider.Transform(text);

			if (result.RenderedText is { } rendered && rendered.Length >= text.Length)
				text = rendered;

			if (result.HighlightSpans is { } highlightSpans)
			{
				foreach (var span in highlightSpans)
				{
					if (span.Start + span.Length <= 0 || span.Length <= 0 || span.Start >= text.Length)
						continue;

					var start = Math.Max(0, span.Start);
					var length = span.Length - (span.Start - start);
					length = Math.Min(length, text.Length - start);
					overrides.Add(new ValueSpan<TextRunProperties>(start, length,
						new GenericTextRunProperties(typeface, FontSize, span.Decorations, span.Brush,
							fontFeatures: FontFeatures)));
				}
			}
		}

		// Password
		if (PasswordChar != default(char) && !RevealPassword)
			text = new string(PasswordChar, text.Length);

		var maxWidth = _constraint.Width > 0 ? _constraint.Width : double.PositiveInfinity;

		return new TextLayout(text, typeface, FontSize, Foreground, TextAlignment, TextWrapping,
			null, null, FlowDirection, maxWidth, double.PositiveInfinity, LineHeight, LetterSpacing,
			0, FontFeatures, overrides, _runCache);
	}
}
