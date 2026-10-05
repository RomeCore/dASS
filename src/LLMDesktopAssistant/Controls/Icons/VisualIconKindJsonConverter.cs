using System.Text.Json;
using System.Text.Json.Serialization;

namespace LLMDesktopAssistant.Controls.Icons
{
	/// <summary>
	/// Serializes <see cref="VisualIconKind"/> as its canonical string form
	/// (<c>&lt;pack&gt;:&lt;data&gt;</c>), or as <see langword="null"/> for
	/// <see cref="VisualIconKind.None"/>.
	/// </summary>
	public class VisualIconKindJsonConverter : JsonConverter<VisualIconKind>
	{
		/// <inheritdoc />
		public override bool HandleNull => true;

		/// <inheritdoc />
		public override VisualIconKind Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return VisualIconKind.None;

			if (reader.TokenType != JsonTokenType.String)
				throw new JsonException("VisualIconKind must be serialized as a string.");

			return VisualIconKind.Parse(reader.GetString());
		}

		/// <inheritdoc />
		public override void Write(Utf8JsonWriter writer, VisualIconKind value, JsonSerializerOptions options)
		{
			if (value.IsNone)
			{
				writer.WriteNullValue();
				return;
			}

			writer.WriteStringValue(value.ToString());
		}
	}
}
