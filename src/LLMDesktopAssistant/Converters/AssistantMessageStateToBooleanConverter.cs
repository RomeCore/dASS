using System.Globalization;
using Avalonia.Data.Converters;
using LLMDesktopAssistant.LLM.MVVM.Messages;

namespace LLMDesktopAssistant.Converters
{
	public class AssistantMessageStateToBooleanConverter : IValueConverter
	{
		public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
		{
			if (value is AssistantMessageState state)
				return state == AssistantMessageState.Processing;
			return value;
		}

		public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}