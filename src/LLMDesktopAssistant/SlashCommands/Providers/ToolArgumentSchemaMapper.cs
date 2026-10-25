using System.Collections.Immutable;
using System.Text.Json.Nodes;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.SlashCommands.Arguments;

namespace LLMDesktopAssistant.SlashCommands.Providers
{
	/// <summary>
	/// Maps a tool's JSON-Schema argument description onto the slash-command argument schema.
	/// </summary>
	/// <remarks>
	/// Every property becomes a <em>keyed</em> command argument. A tool command has no positional slot at all - a call
	/// reads as <c>/tool:fs-read path=README.md</c> - so nothing is ever inferred from the schema's property order.
	/// The declared type picks the format provider: <c>boolean</c>, <c>number</c>/<c>integer</c>, a closed
	/// <c>enum</c>, and <c>array</c>/<c>object</c> (a JSON literal). Everything else - <c>string</c>, a missing or
	/// unknown type, <c>anyOf</c>/<c>oneOf</c>/<c>$ref</c>, nested <c>properties</c> - degrades to a plain string with
	/// no validation; the tool's own argument binding stays the authority on the shape.
	/// </remarks>
	public static class ToolArgumentSchemaMapper
	{
		/// <summary>
		/// Builds the command argument schema for a tool.
		/// </summary>
		/// <param name="schema">The tool's JSON-Schema argument object, or <see langword="null"/>.</param>
		/// <returns>
		/// A keyed-only schema - positionals and the rest slot stay empty - or an empty schema when the tool declares
		/// no properties.
		/// </returns>
		public static SlashCommandArgumentSchema Create(JsonObject? schema)
		{
			if (schema?["properties"] is not JsonObject properties || properties.Count == 0)
				return new SlashCommandArgumentSchema();

			HashSet<string> required = schema["required"] is JsonArray requiredNames
				? requiredNames
					.Select(name => name is JsonValue value && value.TryGetValue<string>(out var text) ? text : null)
					.OfType<string>()
					.ToHashSet(StringComparer.Ordinal)
				: [];

			var builder = ImmutableDictionary.CreateBuilder<string, SlashCommandArgument>(StringComparer.Ordinal);
			foreach (var (name, node) in properties)
			{
				var property = node as JsonObject;

				builder[name] = new SlashCommandArgument
				{
					Name = Locale.GetConstKey(name),
					Description = Describe(property),
					Required = required.Contains(name),
					Default = DefaultOf(property),
					Format = FormatOf(property)
				};
			}

			return new SlashCommandArgumentSchema { Keyed = builder.ToImmutable() };
		}

		/// <summary>
		/// Reads a property's description as raw text: a tool's JSON Schema is authored by the tool, not by the
		/// localization files, so its text is a const key rather than a locale key.
		/// </summary>
		private static LocaleKeyBase? Describe(JsonObject? property) =>
			TextOf(property?["description"]) is { Length: > 0 } text ? Locale.GetConstKey(text) : null;

		/// <summary>
		/// Reads a property's authored default. A string default is taken verbatim (it is a value, not a literal);
		/// every other form keeps its JSON text, which is exactly what its format provider expects of a raw value.
		/// </summary>
		private static string? DefaultOf(JsonObject? property) => property?["default"] switch
		{
			null => null,
			JsonValue value when value.TryGetValue<string>(out var text) => text,
			{ } node => node.ToJsonString()
		};

		/// <summary>
		/// Picks the format provider of a property, or <see langword="null"/> when the value stays a plain,
		/// unvalidated string.
		/// </summary>
		private static ISlashCommandArgumentFormatProvider? FormatOf(JsonObject? property)
		{
			if (property is null)
				return null;

			var type = TypeOf(property);

			if (property["enum"] is JsonArray { Count: > 0 } choices && type is null or "string")
				return new ChoiceArgumentFormatProvider(choices.Select(TextOf), strict: true);

			return type switch
			{
				"boolean" => SlashCommandBooleanFormatProvider.Instance,
				"number" or "integer" => NumberArgumentFormatProvider.Instance,
				"array" or "object" => JsonArgumentFormatProvider.Instance,
				_ => null
			};
		}

		/// <summary>
		/// Reads a property's declared type. A type may legitimately be a union (<c>["string","null"]</c>); the first
		/// entry decides the format, which is the common shape of an optional value.
		/// </summary>
		private static string? TypeOf(JsonObject property) => property["type"] switch
		{
			JsonValue value when value.TryGetValue<string>(out var single) => single,
			JsonArray { Count: > 0 } many => TextOf(many[0]),
			_ => null
		};

		/// <summary>
		/// Reads a JSON scalar as text: a string keeps its value, anything else keeps its JSON text.
		/// </summary>
		private static string TextOf(JsonNode? node) =>
			node is JsonValue value && value.TryGetValue<string>(out var text)
				? text
				: node?.ToJsonString() ?? string.Empty;
	}
}
