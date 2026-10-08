using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using LLMDesktopAssistant.Localization;

namespace LLMDesktopAssistant.Utils.Json
{
	/// <summary>
	/// (De)serializes <see cref="LocaleKeyBase"/> as a discriminated object so that a persisted key survives a restart:
	/// <c>{ "type": "default" | "const" | "formatted", "key": "…", "args": ["…"] }</c> (<c>args</c> only for
	/// <see cref="LocaleFormattedKey"/>). Mirrors the BSON representation in
	/// <c>LiteDB_BSON_SerializerConfig</c>.
	/// </summary>
	public class JsonLocaleKeyConverter : JsonConverter<LocaleKeyBase>
	{
		public override LocaleKeyBase? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return null;

			if (reader.TokenType != JsonTokenType.StartObject)
				throw new JsonException("LocaleKeyBase must be serialized as an object.");

			string? type = null;
			string? key = null;
			ImmutableArray<string?>? args = null;

			while (reader.Read())
			{
				if (reader.TokenType == JsonTokenType.EndObject)
					break;
				if (reader.TokenType != JsonTokenType.PropertyName)
					throw new JsonException("Expected a property name.");

				var propertyName = reader.GetString();
				reader.Read();
				switch (propertyName)
				{
					case "type":
						type = reader.GetString();
						break;
					case "key":
						key = reader.GetString();
						break;
					case "args":
						args = ReadArgs(ref reader);
						break;
					default:
						reader.Skip();
						break;
				}
			}

			if (key is null)
				throw new JsonException("LocaleKeyBase object must contain a 'key' property.");

			return type switch
			{
				"const" => Locale.GetConstKey(key),
				"formatted" => Locale.GetFormattedKey(key, [.. args ?? []]),
				_ => Locale.GetKey(key)
			};
		}

		private static ImmutableArray<string?> ReadArgs(ref Utf8JsonReader reader)
		{
			if (reader.TokenType != JsonTokenType.StartArray)
			{
				reader.Skip();
				return [];
			}

			var builder = ImmutableArray.CreateBuilder<string?>();
			while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
				builder.Add(reader.TokenType == JsonTokenType.Null ? null : reader.GetString());
			return builder.ToImmutable();
		}

		public override void Write(Utf8JsonWriter writer, LocaleKeyBase value, JsonSerializerOptions options)
		{
			writer.WriteStartObject();

			switch (value)
			{
				case ConstLocaleKey:
					writer.WriteString("type", "const");
					writer.WriteString("key", value.Key);
					break;

				case LocaleFormattedKey formatted:
					writer.WriteString("type", "formatted");
					writer.WriteString("key", value.Key);
					writer.WriteStartArray("args");
					foreach (var arg in formatted.FormatArgs)
					{
						if (arg is null)
							writer.WriteNullValue();
						else
							writer.WriteStringValue(arg);
					}
					writer.WriteEndArray();
					break;

				default:
					writer.WriteString("type", "default");
					writer.WriteString("key", value.Key);
					break;
			}

			writer.WriteEndObject();
		}
	}
}
