using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia.Media;

namespace LLMDesktopAssistant.Styles.Theming
{
	/// <summary>
	/// Stores an Avalonia colour as a hex string such as <c>#FF6C5CE7</c> in the startup settings.
	/// </summary>
	/// <remarks>
	/// The startup settings are read before the reflection scan, so they use plain JSON options: a
	/// struct like <see cref="Color"/> has no built-in converter there, and this one keeps the file
	/// readable and hand-editable.
	/// </remarks>
	public class ColorJsonConverter : JsonConverter<Color>
	{
		/// <inheritdoc/>
		public override Color Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
			=> Color.TryParse(reader.GetString(), out var color) ? color : default;

		/// <inheritdoc/>
		public override void Write(Utf8JsonWriter writer, Color value, JsonSerializerOptions options)
			=> writer.WriteStringValue(value.ToString());
	}
}
