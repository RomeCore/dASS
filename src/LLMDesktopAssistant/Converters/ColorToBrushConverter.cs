using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace LLMDesktopAssistant.Converters
{
	/// <summary>
	/// Converts a nullable <see cref="Color"/> into a brush so it can be bound to
	/// <see cref="IBrush"/> properties (e.g. BorderBrush / Foreground).
	/// Null is passed through as null so <c>TargetNullValue</c> can kick in.
	/// </summary>
	/// <remarks>
	/// The optional parameter multiplies the alpha channel (a number from 0 to 1), which is how a surface asks
	/// for a quieter border from the same color without a second property on the model.
	/// </remarks>
	public class ColorToBrushConverter : IValueConverter
	{
		public static ColorToBrushConverter Instance { get; } = new ColorToBrushConverter();

		public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
		{
			if (value is not Color color)
				return null;

			if (parameter is string text
				&& double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var alpha))
				color = Color.FromArgb((byte)(Math.Clamp(alpha, 0, 1) * 255), color.R, color.G, color.B);

			return new ImmutableSolidColorBrush(color);
		}

		public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
			=> value is ISolidColorBrush brush ? brush.Color : null;
	}
}
