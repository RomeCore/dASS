using System.Reflection;
using System.Text.Json.Nodes;
using LLMDesktopAssistant.Tools.Specifiers;
using RCLargeLanguageModels.Json;
using RCLargeLanguageModels.Json.Schema;

namespace LLMDesktopAssistant.Tools.Execution.Domain
{
	/// <summary>
	/// One argument of a tool: its JSON name, the schema the model sees, the schema it was authored
	/// with (metadata included) and, when the tool is generated from a C# method, the parameter it came
	/// from. The argument is the single description of one value, so the model-facing schema, the
	/// slash-command format and the specifier dialect all read it instead of re-deriving anything.
	/// </summary>
	public sealed class ToolArgumentDescriptor
	{
		/// <summary>
		/// The metadata key that marks the main argument inside a schema.
		/// </summary>
		public const string MainMetadataKey = "main";

		/// <summary>
		/// The name of the argument in the JSON schema. This is also the name a specifier rule writes
		/// before the colon, which makes it a persistent contract: renaming it breaks saved rules.
		/// </summary>
		public required string Name { get; init; }

		/// <summary>
		/// The description the model sees, or <see langword="null"/>.
		/// </summary>
		public string? Description { get; init; }

		/// <summary>
		/// The name of the C# parameter the argument was generated from, or <see langword="null"/> when
		/// the argument was not generated from a method (a scripted or MCP tool).
		/// </summary>
		public string? ClrName { get; init; }

		/// <summary>
		/// The C# parameter the argument was generated from, or <see langword="null"/>.
		/// </summary>
		public ParameterInfo? ClrParameter { get; init; }

		/// <summary>
		/// The accessor the argument was generated from, or <see langword="null"/>. It carries the
		/// default value and the required flag the binder applies to a call.
		/// </summary>
		public JsonMemberAccessor? ClrParameterAccessor { get; init; }

		/// <summary>
		/// Whether the model must supply the argument.
		/// </summary>
		public bool Required { get; init; }

		/// <summary>
		/// The position of the argument in the tool's declaration, which is the order of the
		/// <c>properties</c> object in the generated schema.
		/// </summary>
		public int Order { get; init; }

		/// <summary>
		/// The shape of the argument.
		/// </summary>
		public ToolArgumentKind Kind { get; init; }

		/// <summary>
		/// Whether the argument is the main argument of the tool.
		/// </summary>
		public bool IsMain { get; init; }

		/// <summary>
		/// The schema with the dASS metadata intact - the authoring form, kept so a later consumer can
		/// still see the declared kind and the main-argument mark.
		/// </summary>
		public required JsonObject OriginalSchema { get; init; }

		/// <summary>
		/// The schema the model sees: <see cref="OriginalSchema"/> without the dASS metadata.
		/// </summary>
		public required JsonObject Schema { get; init; }

		/// <summary>
		/// The authored default of the argument, or <see langword="null"/>. The raw JSON node is what a
		/// tool executor expects, and it is what a missing argument is filled with.
		/// </summary>
		public JsonNode? Default => Schema["default"];

		/// <summary>
		/// The dialect a specifier literal is compiled in when it targets this argument, or
		/// <see langword="null"/> when the argument cannot be a specifier target.
		/// </summary>
		public SpecifierTargetKind? SpecifierKind => ToolArgumentKinds.ToSpecifierKind(Kind);

		/// <summary>
		/// Describes a C# method parameter: generates its schema, records the declared metadata and
		/// trims it from the model-facing copy.
		/// </summary>
		/// <param name="accessor">The parameter accessor.</param>
		/// <param name="order">The position of the parameter in the tool's declaration.</param>
		/// <param name="modifySchema">
		/// The tool's per-argument hook, applied before the model-facing copy is trimmed so the two can
		/// never disagree. It receives the JSON name of the argument and may narrow values or rewrite
		/// the description; the type and the dASS metadata stay the descriptor's.
		/// </param>
		/// <returns>The argument descriptor.</returns>
		public static ToolArgumentDescriptor FromClr(JsonMemberAccessor accessor, int order,
			Action<string, JsonObject>? modifySchema = null)
		{
			ArgumentNullException.ThrowIfNull(accessor);

			var original = JsonSchemaGenerator.Generate(accessor) as JsonObject
				?? throw new InvalidOperationException($"The generated schema of '{accessor.Name}' is not a JSON object.");

			var parameter = accessor.Parameter;
			var isMain = parameter?.IsDefined(typeof(MainArgumentAttribute), inherit: true) ?? false;
			var authored = GetAuthoredKind(parameter);

			// A path declared on something that is not a string is a bug in the tool, not a runtime
			// condition: the kind would silently stop matching anything the model can send.
			var declaredType = ToolArgumentKinds.DeclaredType(original);
			var impliedType = ToolArgumentKinds.ImpliedJsonType(authored);
			if (impliedType is not null && declaredType is not null && declaredType != impliedType)
				throw new ArgumentException(
					$"The argument '{accessor.Name}' is declared as a path but its schema type is '{declaredType}'.",
					nameof(accessor));

			modifySchema?.Invoke(accessor.Name, original);

			// A hook that redefines the type would leave the kind and the binder's conversion lying.
			var typeAfter = ToolArgumentKinds.DeclaredType(original);
			if (!string.Equals(declaredType, typeAfter, StringComparison.Ordinal))
				throw new ArgumentException(
					$"The schema hook of '{accessor.Name}' changed its type from '{declaredType}' to '{typeAfter}'; the type belongs to the descriptor.",
					nameof(modifySchema));

			// The metadata is ours: a hook that writes it would be trimmed away and silently lost.
			original.Remove(MainMetadataKey);
			original.Remove(ToolArgumentKinds.MetadataKey);

			if (isMain)
				original[MainMetadataKey] = true;
			if (ToolArgumentKinds.ToMetadataValue(authored) is { } kindMetadata)
				original[ToolArgumentKinds.MetadataKey] = kindMetadata;

			return new ToolArgumentDescriptor
			{
				Name = accessor.Name,
				Description = ReadDescription(original),
				ClrName = parameter?.Name,
				ClrParameter = parameter,
				ClrParameterAccessor = accessor,
				Required = accessor.Required,
				Order = order,
				Kind = authored is ToolArgumentKind.Unspecified ? ToolArgumentKinds.FromSchema(original) : authored,
				IsMain = isMain,
				OriginalSchema = original,
				Schema = Trim(original)
			};
		}

		/// <summary>
		/// Describes a tool that takes one bare value: the whole schema <em>is</em> the argument, because
		/// the tool's arguments are not a JSON object. There is no JSON name, so <see cref="Name"/> is
		/// empty on purpose - nothing can address it, and an unnamed specifier literal is what targets it.
		/// </summary>
		/// <param name="schema">The root schema, which is also the argument's schema.</param>
		public static ToolArgumentDescriptor FromRoot(JsonObject schema)
		{
			ArgumentNullException.ThrowIfNull(schema);

			var original = schema.DeepClone().AsObject();

			return new ToolArgumentDescriptor
			{
				Name = string.Empty,
				Description = ReadDescription(original),
				Order = 0,
				Kind = ToolArgumentKinds.FromSchema(original),
				IsMain = true,
				OriginalSchema = original,
				Schema = Trim(original)
			};
		}

		/// <summary>
		/// Describes an argument that was authored as a JSON Schema property instead of generated from a
		/// C# method - a scripted tool's frontmatter, an MCP server. The dASS metadata, when the author
		/// wrote any, is read and trimmed exactly as it is for a generated argument.
		/// </summary>
		/// <param name="name">The JSON name of the argument.</param>
		/// <param name="schema">The property schema as authored.</param>
		/// <param name="required">Whether the schema lists the argument as required.</param>
		/// <param name="order">The position of the argument among the schema's properties.</param>
		/// <returns>The argument descriptor.</returns>
		public static ToolArgumentDescriptor FromJson(string name, JsonObject schema, bool required, int order)
		{
			ArgumentNullException.ThrowIfNull(name);
			ArgumentNullException.ThrowIfNull(schema);

			var original = schema.DeepClone().AsObject();

			return new ToolArgumentDescriptor
			{
				Name = name,
				Description = ReadDescription(original),
				Required = required,
				Order = order,
				Kind = ToolArgumentKinds.FromSchema(original),
				IsMain = original[MainMetadataKey] is JsonValue mark && mark.TryGetValue<bool>(out var isMain) && isMain,
				OriginalSchema = original,
				Schema = Trim(original)
			};
		}

		/// <summary>
		/// Reads a schema's description, or <see langword="null"/> when it declares none.
		/// </summary>
		/// <param name="schema">The schema to read.</param>
		private static string? ReadDescription(JsonObject schema) =>
			schema["description"] is JsonValue description && description.TryGetValue<string>(out var text)
				? text
				: null;

		/// <summary>
		/// Reads the kind a parameter declares beyond its JSON type. Path-ness is the only thing an
		/// attribute can say: everything else is the generated type's business.
		/// </summary>
		/// <param name="parameter">The parameter, or <see langword="null"/> for a non-C# argument.</param>
		private static ToolArgumentKind GetAuthoredKind(ParameterInfo? parameter)
		{
			if (parameter?.GetCustomAttribute<PathArgumentAttribute>() is not { } path)
				return ToolArgumentKind.Unspecified;

			return (path.File, path.Directory) switch
			{
				(true, true) => ToolArgumentKind.FileOrDirectory,
				(true, false) => ToolArgumentKind.FilePath,
				(false, true) => ToolArgumentKind.DirectoryPath,
				_ => throw new ArgumentException(
					$"The parameter '{parameter.Name}' is declared as a path argument that is neither a file nor a directory.",
					nameof(parameter))
			};
		}

		/// <summary>
		/// Copies the authored schema without the dASS metadata, which never reaches the model.
		/// </summary>
		private static JsonObject Trim(JsonObject schema)
		{
			var trimmed = schema.DeepClone().AsObject();
			trimmed.Remove(MainMetadataKey);
			trimmed.Remove(ToolArgumentKinds.MetadataKey);
			return trimmed;
		}
	}
}
