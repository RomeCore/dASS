using System.Globalization;
using System.Text;

namespace LLMDesktopAssistant.StructuredValues.Converters
{
	/// <summary>
	/// Serializes a structured node value (<see cref="INodeValue"/>) into a Python literal, ready to be embedded into
	/// generated Python code (e.g. <c>command_args = { ... }</c>).
	/// </summary>
	/// <remarks>
	/// Forward-only: Python source is never parsed back. Numbers are emitted as integers when integral so values do not
	/// get mangled into <c>3.0</c>; strings are single-quoted with minimal escaping; nulls become <c>None</c>.
	/// </remarks>
	public static class PythonStructuredSerializer
	{
		/// <summary>Serializes the given node value into a Python literal.</summary>
		public static string ToPythonString(this INodeValue? value)
		{
			var builder = new StringBuilder();
			Write(builder, value);
			return builder.ToString();
		}

		private static void Write(StringBuilder builder, INodeValue? value)
		{
			switch (value)
			{
				case null:
				case INodeNullValue:
					builder.Append("None");
					break;

				case INodeBooleanValue boolean:
					builder.Append(boolean.Value ? "True" : "False");
					break;

				case INodeNumberValue number:
					builder.Append(FormatNumber(number.Value));
					break;

				case INodeStringValue text:
					builder.Append(Quote(text.Value ?? string.Empty));
					break;

				case INodeArrayValue array:
					builder.Append('[');
					for (int i = 0; i < array.Items.Count; i++)
					{
						if (i > 0)
							builder.Append(", ");
						Write(builder, array.Items[i]);
					}
					builder.Append(']');
					break;

				case INodeDictionaryValue dictionary:
					builder.Append('{');
					var first = true;
					foreach (var (key, item) in dictionary.Items)
					{
						if (!first)
							builder.Append(", ");
						first = false;
						builder.Append(Quote(key)).Append(": ");
						Write(builder, item);
					}
					builder.Append('}');
					break;

				default:
					builder.Append("None");
					break;
			}
		}

		private static string FormatNumber(double value)
		{
			if (value == Math.Truncate(value) && !double.IsInfinity(value) && Math.Abs(value) < 1e15)
				return ((long)value).ToString(CultureInfo.InvariantCulture);

			return value.ToString("R", CultureInfo.InvariantCulture);
		}

		private static string Quote(string value)
		{
			var builder = new StringBuilder(value.Length + 2);
			builder.Append('\'');

			foreach (var ch in value)
			{
				switch (ch)
				{
					case '\\':
						builder.Append("\\\\");
						break;
					case '\'':
						builder.Append("\\'");
						break;
					case '\n':
						builder.Append("\\n");
						break;
					case '\r':
						builder.Append("\\r");
						break;
					case '\t':
						builder.Append("\\t");
						break;
					default:
						builder.Append(ch);
						break;
				}
			}

			builder.Append('\'');
			return builder.ToString();
		}
	}
}
