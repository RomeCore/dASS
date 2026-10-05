using System.ComponentModel;
using System.Globalization;

namespace LLMDesktopAssistant.Controls.Icons
{
	/// <summary>
	/// Converts <see cref="VisualIconKind"/> to and from its canonical string form for XAML
	/// (<c>Kind="material:Account"</c>, <c>Kind="path:M0 0 L24 24"</c>, or a bare
	/// <c>Kind="Account"</c> shorthand for the material pack).
	/// </summary>
	public class VisualIconKindAvaloniaConverter : TypeConverter
	{
		/// <inheritdoc />
		public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
			=> sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

		/// <inheritdoc />
		public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
			=> destinationType == typeof(string) || base.CanConvertTo(context, destinationType);

		/// <inheritdoc />
		public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
			=> value is string text ? VisualIconKind.Parse(text) : base.ConvertFrom(context, culture, value);

		/// <inheritdoc />
		public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
			=> destinationType == typeof(string) && value is VisualIconKind icon
				? icon.ToString()
				: base.ConvertTo(context, culture, value, destinationType);
	}
}
