using System.Globalization;
using Avalonia.Data.Converters;

namespace LLMDesktopAssistant.Converters
{
	/// <summary>
	/// Converts a boolean value to an opacity value.
	/// Returns <see cref="TrueOpacity"/> when <see langword="true"/> (enabled)
	/// and <see cref="FalseOpacity"/> when <see langword="false"/> (disabled).
	/// </summary>
	public class BooleanToOpacityConverter : IValueConverter
	{
		/// <summary>
		/// The opacity value when the boolean is <see langword="true"/> (default: 1.0).
		/// </summary>
		public double TrueOpacity { get; set; } = 1.0;

		/// <summary>
		/// The opacity value when the boolean is <see langword="false"/> (default: 0.0).
		/// </summary>
		public double FalseOpacity { get; set; } = 0.0;

		public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
		{
			if (value is bool bValue)
			{
				return bValue ? TrueOpacity : FalseOpacity;
			}
			return TrueOpacity;
		}

		public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}
}
