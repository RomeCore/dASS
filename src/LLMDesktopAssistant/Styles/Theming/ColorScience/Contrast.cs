using Avalonia.Media;

namespace LLMDesktopAssistant.Styles.Theming.ColorScience
{
	/// <summary>
	/// WCAG contrast helpers used to pick the text colour that sits on a filled surface.
	/// </summary>
	public static class Contrast
	{
		private const double minimumRatio = 4.5;

		private static readonly Color black = Color.FromArgb(255, 0, 0, 0);
		private static readonly Color white = Color.FromArgb(255, 255, 255, 255);

		/// <summary>
		/// Computes the WCAG contrast ratio between two colours.
		/// </summary>
		/// <param name="first">The first colour.</param>
		/// <param name="second">The second colour.</param>
		/// <returns>A ratio between 1 and 21.</returns>
		public static double Ratio(Color first, Color second)
		{
			var a = RelativeLuminance(first);
			var b = RelativeLuminance(second);

			return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
		}

		/// <summary>
		/// Picks black or white, whichever reads better on the given background.
		/// </summary>
		/// <param name="background">The surface the text sits on.</param>
		/// <returns>Black when it clears the WCAG AA ratio, white otherwise.</returns>
		/// <remarks>
		/// The naive lightness threshold used by most palettes picks white on mid yellows and greens,
		/// where white text is unreadable; this compares the real ratios instead.
		/// </remarks>
		public static Color PickOn(Color background)
			=> Ratio(background, black) >= minimumRatio ? black : white;

		/// <summary>
		/// Computes the WCAG relative luminance of a colour.
		/// </summary>
		/// <param name="color">The colour to measure.</param>
		/// <returns>A value between 0 and 1.</returns>
		public static double RelativeLuminance(Color color)
		{
			var r = Linearize(color.R / 255.0);
			var g = Linearize(color.G / 255.0);
			var b = Linearize(color.B / 255.0);

			return 0.2126 * r + 0.7152 * g + 0.0722 * b;
		}

		private static double Linearize(double channel)
			=> channel <= 0.04045 ? channel / 12.92 : Math.Pow((channel + 0.055) / 1.055, 2.4);
	}
}
