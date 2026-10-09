using Avalonia.Media;

namespace LLMDesktopAssistant.Controls.Text;

/// <summary>
/// A text highlight range (in characters, coordinates of the original text, ghost not included).
/// </summary>
/// <param name="Start">The 0-based index of the range's first character.</param>
/// <param name="Length">The range's length.</param>
/// <param name="Brush">The range's foreground brush.</param>
/// <param name="Decorations">The range's decorations, if any.</param>
/// <param name="Background">The range's background brush, if any — a chip behind the text.</param>
public readonly record struct TextHighlightSpan(int Start, int Length, IBrush Brush,
	TextDecorationCollection? Decorations = null, IBrush? Background = null);
