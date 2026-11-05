using System.Text.Json.Nodes;
using LLMDesktopAssistant.Tools.Specifiers;

namespace LLMDesktopAssistant.Tools.Execution.Domain
{
	/// <summary>
	/// Classifies a tool argument from its schema: the dASS <c>kind</c> metadata first, the JSON Schema
	/// <c>type</c> second. This is the single classifier every consumer reads - the slash-command
	/// autocomplete format and the specifier dialect included - so the two cannot disagree.
	/// </summary>
	public static class ToolArgumentKinds
	{
		/// <summary>
		/// The metadata key that carries an authored <see cref="ToolArgumentKind"/> inside a schema.
		/// </summary>
		public const string MetadataKey = "kind";

		/// <summary>
		/// The metadata value of <see cref="ToolArgumentKind.FilePath"/>.
		/// </summary>
		public const string FileMetadataValue = "file";

		/// <summary>
		/// The metadata value of <see cref="ToolArgumentKind.DirectoryPath"/>.
		/// </summary>
		public const string DirectoryMetadataValue = "directory";

		/// <summary>
		/// The metadata value of <see cref="ToolArgumentKind.FileOrDirectory"/>.
		/// </summary>
		public const string FileOrDirectoryMetadataValue = "fileOrDirectory";

		/// <summary>
		/// Classifies an argument schema.
		/// </summary>
		/// <param name="schema">The argument schema, with its metadata intact.</param>
		/// <returns>The kind of the argument, or <see cref="ToolArgumentKind.Unspecified"/>.</returns>
		public static ToolArgumentKind FromSchema(JsonObject schema)
		{
			ArgumentNullException.ThrowIfNull(schema);

			return TryFromMetadata(schema[MetadataKey], out var authored)
				? authored
				: FromJsonType(DeclaredType(schema));
		}

		/// <summary>
		/// Reads an authored kind from its metadata value.
		/// </summary>
		/// <param name="node">The metadata node, or <see langword="null"/>.</param>
		/// <param name="kind">The kind the metadata names.</param>
		/// <returns><see langword="true"/> when the node is a known metadata value.</returns>
		public static bool TryFromMetadata(JsonNode? node, out ToolArgumentKind kind)
		{
			var text = node is JsonValue value && value.TryGetValue<string>(out var authored) ? authored : null;

			switch (text)
			{
				case FileMetadataValue:
					kind = ToolArgumentKind.FilePath;
					return true;
				case DirectoryMetadataValue:
					kind = ToolArgumentKind.DirectoryPath;
					return true;
				case FileOrDirectoryMetadataValue:
					kind = ToolArgumentKind.FileOrDirectory;
					return true;
				default:
					kind = ToolArgumentKind.Unspecified;
					return false;
			}
		}

		/// <summary>
		/// Returns the metadata value that carries the kind, or <see langword="null"/> when the kind is
		/// already implied by the JSON type and needs no metadata.
		/// </summary>
		/// <param name="kind">The kind to encode.</param>
		public static string? ToMetadataValue(ToolArgumentKind kind) => kind switch
		{
			ToolArgumentKind.FilePath => FileMetadataValue,
			ToolArgumentKind.DirectoryPath => DirectoryMetadataValue,
			ToolArgumentKind.FileOrDirectory => FileOrDirectoryMetadataValue,
			_ => null
		};

		/// <summary>
		/// Maps a JSON Schema type name onto a kind.
		/// </summary>
		/// <param name="type">The declared type, or <see langword="null"/>.</param>
		public static ToolArgumentKind FromJsonType(string? type) => type switch
		{
			"string" => ToolArgumentKind.String,
			"boolean" => ToolArgumentKind.Bool,
			"integer" => ToolArgumentKind.Integer,
			"number" => ToolArgumentKind.Number,
			"array" => ToolArgumentKind.Array,
			"object" => ToolArgumentKind.Object,
			_ => ToolArgumentKind.Unspecified
		};

		/// <summary>
		/// Reads the declared JSON type of a schema, skipping the <c>"null"</c> entry of a union type.
		/// </summary>
		/// <param name="schema">The schema to read.</param>
		/// <returns>The first non-null declared type, or <see langword="null"/>.</returns>
		public static string? DeclaredType(JsonObject schema)
		{
			switch (schema["type"])
			{
				case JsonValue value when value.TryGetValue<string>(out var single):
					return single;

				case JsonArray many:
					foreach (var entry in many)
					{
						if (entry is JsonValue item && item.TryGetValue<string>(out var type) && type != "null")
							return type;
					}

					return null;

				default:
					return null;
			}
		}

		/// <summary>
		/// Returns the JSON type an authored kind implies, used to reject a metadata declaration that
		/// contradicts the schema it sits in.
		/// </summary>
		/// <param name="kind">The authored kind.</param>
		public static string? ImpliedJsonType(ToolArgumentKind kind) => kind switch
		{
			ToolArgumentKind.String or ToolArgumentKind.FilePath or ToolArgumentKind.DirectoryPath
				or ToolArgumentKind.FileOrDirectory => "string",
			ToolArgumentKind.Bool => "boolean",
			ToolArgumentKind.Integer => "integer",
			ToolArgumentKind.Number => "number",
			ToolArgumentKind.Array => "array",
			ToolArgumentKind.Object => "object",
			_ => null
		};

		/// <summary>
		/// The dialect a specifier literal is compiled in when it targets an argument of this kind, or
		/// <see langword="null"/> when the argument is not a specifier target at all: arrays and objects
		/// are values, not prose, so a rule can never sensibly name them.
		/// </summary>
		/// <param name="kind">The kind of the argument.</param>
		public static SpecifierTargetKind? ToSpecifierKind(ToolArgumentKind kind) => kind switch
		{
			ToolArgumentKind.FilePath or ToolArgumentKind.DirectoryPath or ToolArgumentKind.FileOrDirectory
				=> SpecifierTargetKind.Path,
			ToolArgumentKind.String or ToolArgumentKind.Bool or ToolArgumentKind.Integer or ToolArgumentKind.Number
				=> SpecifierTargetKind.Text,
			_ => null
		};
	}
}
