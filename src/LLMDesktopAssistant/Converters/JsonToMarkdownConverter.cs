using System.Globalization;
using System.Text.Json.Nodes;
using Avalonia.Data.Converters;
using LLMDesktopAssistant.LLM.MVVM.Messages;
using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.Converters
{
	public class JsonToMarkdownConverter : IValueConverter
	{
		public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
		{
			if (value is JsonNode node)
			{
				try
				{
					return ToolCallArgumentFormatter.FormatToMarkdown(node);
				}
				catch
				{
					return $"```json\n{node.ToJsonString()}\n```";
				}
			}
			else if (value is string json)
			{
				try
				{
					node = TolerantJsonParser.Parse(json)!;
					return ToolCallArgumentFormatter.FormatToMarkdown(node);
				}
				catch
				{
					return $"```json\n{json}\n```";
				}
			}
			return value;
		}

		public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}