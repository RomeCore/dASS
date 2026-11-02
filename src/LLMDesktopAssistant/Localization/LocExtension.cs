using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;
using LLMDesktopAssistant.Services;

namespace LLMDesktopAssistant.Localization
{
	public class LocExtension : MarkupExtension
	{
		private readonly object? _key;
		private readonly BindingBase? _binding;
		private object? _targetObject;
		private object? _targetProperty;

		/// <summary>
		/// Optional string format.
		/// </summary>
		public string? StringFormat { get; set; }

		/// <summary>
		/// Optional converter for additional processing.
		/// </summary>
		public IValueConverter? Converter { get; set; }

		/// <summary>
		/// Converter parameter.
		/// </summary>
		public object? ConverterParameter { get; set; }

		/// <summary>
		/// Initializes a new instance with a key.
		/// </summary>
		public LocExtension(object key)
		{
			_key = key;
			_binding = key as BindingBase;
		}

		public override object ProvideValue(IServiceProvider serviceProvider)
		{
			var provideValueTarget = serviceProvider.GetService(typeof(IProvideValueTarget)) as IProvideValueTarget;
			_targetObject = provideValueTarget?.TargetObject;
			_targetProperty = provideValueTarget?.TargetProperty;

			if (_targetObject == null || _targetProperty == null)
			{
				return this;
			}

			var multiBinding = new MultiBinding();

			if (_binding != null)
			{
				multiBinding.Bindings.Add(_binding);
			}
			else
			{
				multiBinding.Bindings.Add(new Binding
				{
					Source = _key,
					Mode = BindingMode.OneTime
				});
			}

			if (LocalizationManager.Current is { } localizationManager)
			{
				multiBinding.Bindings.Add(new Binding
				{
					Source = localizationManager,
					Path = "CurrentLanguage",
					Mode = BindingMode.OneWay
				});
			}

			multiBinding.Converter = new LocalizationConverter();
			multiBinding.ConverterParameter = StringFormat;

			return multiBinding;
		}

		private class LocalizationConverter : IMultiValueConverter
		{
			public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
			{
				if (values.Count == 0) return null;

				var key = values[0]?.ToString();
				if (string.IsNullOrEmpty(key)) return null;

				var localizedValue = values[0] is LocaleKeyBase localeKey
					? localeKey.Value
					: LocalizationManager.LocalizeStatic(key);

				if (parameter is string format && !string.IsNullOrEmpty(format))
				{
					localizedValue = string.Format(format, localizedValue);
				}

				return localizedValue;
			}

			public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
				=> throw new NotSupportedException();
		}
	}
}