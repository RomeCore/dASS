using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

namespace LLMDesktopAssistant.LLM.MVVM.Messages
{
	public static class ToolCallArgumentFormatter
	{
		private static readonly JsonSerializerOptions _toolCallSerializerOptions = new JsonSerializerOptions
		{
			TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
			Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
			WriteIndented = true
		};

		public static string? FormatToJson(JsonNode? arguments)
		{
			if (arguments == null)
				return null;

			return arguments.ToJsonString(_toolCallSerializerOptions);
		}


		public static string FormatToMarkdown(JsonNode? arguments)
		{
			if (arguments == null)
				return "_no arguments_";

			return arguments switch
			{
				JsonObject obj => FormatObject(obj),
				JsonArray arr => FormatArray(arr),
				_ => FormatValue(arguments)
			};
		}

		private static string FormatObject(JsonObject obj)
		{
			if (obj.Count == 0)
				return "_empty object_";

			var sb = new StringBuilder();

			foreach (var kv in obj)
			{
				sb.Append("- **").Append(kv.Key).Append("**: ");

				if (kv.Value is JsonObject or JsonArray)
				{
					sb.AppendLine();
					sb.AppendLine(Indent(FormatToMarkdown(kv.Value)));
				}
				else
				{
					sb.AppendLine(FormatValue(kv.Value, kv.Key));
				}
			}

			return sb.ToString();
		}

		private static string FormatArray(JsonArray arr)
		{
			if (arr.Count == 0)
				return "_empty array_";

			var sb = new StringBuilder();

			for (int i = 0; i < arr.Count; i++)
			{
				var item = arr[i];

				sb.Append("- ");

				if (item is JsonObject or JsonArray)
				{
					sb.AppendLine();
					sb.AppendLine(Indent(FormatToMarkdown(item)));
				}
				else
				{
					sb.AppendLine(FormatValue(item));
				}
			}

			return sb.ToString();
		}

		private static string FormatValue(JsonNode? value, string? key = null)
		{
			if (value == null)
				return "`null`";

			if (value is JsonValue jsonValue)
			{
				var raw = jsonValue.GetValueKind() == JsonValueKind.String ?
					jsonValue.GetValue<string>() :
					value.ToJsonString(_toolCallSerializerOptions);

				if (raw.StartsWith("\"") && raw.EndsWith("\""))
					raw = raw[1..^1];

				bool isMultiline = raw.Contains('\n') || raw.Length > 80;

				if (isMultiline)
				{
					string lang = key switch
					{
						"python" => "python",
						"shell" => "bash",
						"lua" => "lua",
						_ => key ?? string.Empty
					};

					return "\n" + Indent($"```{lang}\n{raw}\n```");
				}

				return $"`{raw}`";
			}

			return $"```json\n{value.ToJsonString(_toolCallSerializerOptions)}\n```";
		}

		private static string Indent(string text, int level = 1)
		{
			var indent = new string(' ', level * 2);
			var lines = text.Split('\n');

			return string.Join("\n", lines.Select(l => indent + l));
		}
	}
}