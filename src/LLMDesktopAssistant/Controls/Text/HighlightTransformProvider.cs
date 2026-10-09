namespace LLMDesktopAssistant.Controls.Text;

/// <summary>
/// A delegate-based <see cref="IHighlightTransformProvider"/>, similar to <see cref="RelayCommand"/>
/// in CommunityToolkit.Mvvm: the transform logic is supplied via a delegate and the owner calls
/// <see cref="NotifyLayoutChanged"/> when the provider state changed without a text change.
/// </summary>
public sealed class HighlightTransformProvider : IHighlightTransformProvider
{
	private readonly Func<string, int, HighlightTransformResult> _transform;

	/// <summary>
	/// Creates a provider that computes the result with the specified delegate.
	/// </summary>
	public HighlightTransformProvider(Func<string, int, HighlightTransformResult> transform)
	{
		_transform = transform ?? throw new ArgumentNullException(nameof(transform));
	}

	/// <inheritdoc/>
	public event EventHandler? LayoutChanged
	{
		add => _layoutChanged += value;
		remove => _layoutChanged -= value;
	}

	private event EventHandler? _layoutChanged;

	/// <inheritdoc/>
	public HighlightTransformResult Transform(string text, int caretIndex) => _transform(text, caretIndex);

	/// <summary>
	/// Notifies subscribers that the visual output changed and the layout must be recomputed.
	/// </summary>
	public void NotifyLayoutChanged() => _layoutChanged?.Invoke(this, EventArgs.Empty);
}
