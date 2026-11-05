using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Nodes;

namespace LLMDesktopAssistant.Tools.Execution.Domain
{
	/// <summary>
	/// The arguments of one tool, in declaration order, plus the main argument. This is the only
	/// description of a tool's arguments: the model-facing JSON schema is generated from it, and every
	/// other consumer - formatting, specifier matching, the tool card - reads it instead of the schema.
	/// </summary>
	public sealed class ToolArgumentsDescriptor
	{
		/// <summary>
		/// The arguments of a tool that takes none.
		/// </summary>
		public static ToolArgumentsDescriptor Empty { get; } = new();

		/// <summary>
		/// The arguments, sorted by <see cref="ToolArgumentDescriptor.Order"/>.
		/// </summary>
		public ImmutableArray<ToolArgumentDescriptor> Arguments { get; init; } = [];

		/// <summary>
		/// The JSON name of the main argument, or <see langword="null"/> when the tool has none.
		/// </summary>
		public string? MainName { get; init; }

		/// <summary>
		/// The authored root schema, when the argument set came from one - a scripted tool's frontmatter,
		/// an MCP server. Kept so the keys that do not describe an argument (<c>required</c> order,
		/// <c>$defs</c>, <c>additionalProperties</c>, <c>title</c>) reach the model exactly as they were
		/// written. <see langword="null"/> for a tool generated from a C# method.
		/// </summary>
		public JsonObject? RootSchema { get; init; }

		/// <summary>
		/// Whether the argument set is a JSON object. <see langword="false"/> means the tool takes a
		/// single bare value, carried by the main argument; only a converted schema (a scripted or MCP
		/// tool) can declare that, never a schema generated from a C# method.
		/// </summary>
		public bool IsObject { get; init; } = true;

		/// <summary>
		/// The main argument, or <see langword="null"/> when the tool has none.
		/// </summary>
		public ToolArgumentDescriptor? Main => MainName is { } name && TryGet(name, out var main) ? main : null;

		/// <summary>
		/// Whether any argument of the tool can be a specifier target: the main argument, which an unnamed
		/// literal covers, or a nameable one. A tool whose arguments are all arrays and objects has no
		/// rules worth evaluating, and no specifier analyzer.
		/// </summary>
		public bool HasSpecifierTargets => Main is not null || SpecifierParameters.Any();

		/// <summary>
		/// The arguments a specifier rule can reference by name, in declaration order. Arrays and objects
		/// are excluded - they are values, not prose - and so is the main argument: an unnamed literal
		/// already targets it, and a second name for it would be a competing view of the same value.
		/// </summary>
		public IEnumerable<ToolArgumentDescriptor> SpecifierParameters =>
			Arguments.Where(argument => !argument.IsMain && argument.SpecifierKind is not null);

		/// <summary>
		/// Finds an argument by its JSON name.
		/// </summary>
		/// <param name="name">The JSON name of the argument.</param>
		/// <param name="descriptor">The argument, or <see langword="null"/> when there is no such name.</param>
		/// <returns><see langword="true"/> when the argument exists.</returns>
		public bool TryGet(string name, [NotNullWhen(true)] out ToolArgumentDescriptor? descriptor)
		{
			foreach (var argument in Arguments)
			{
				if (string.Equals(argument.Name, name, StringComparison.Ordinal))
				{
					descriptor = argument;
					return true;
				}
			}

			descriptor = null;
			return false;
		}

		/// <summary>
		/// Builds the descriptor from a JSON Schema authored elsewhere - a scripted tool's frontmatter, an
		/// MCP server - instead of from a C# method.
		/// </summary>
		/// <param name="schema">The authored argument schema.</param>
		/// <returns>
		/// The arguments, in the order the schema lists them. A root that is not an object becomes one bare
		/// argument - the root itself - with <see cref="IsObject"/> set to <see langword="false"/>.
		/// </returns>
		public static ToolArgumentsDescriptor FromSchema(JsonNode schema)
		{
			ArgumentNullException.ThrowIfNull(schema);

			var root = schema as JsonObject
				?? throw new ArgumentException("A tool argument schema must be a JSON object node.", nameof(schema));

			if (ToolArgumentKinds.DeclaredType(root) is { } rootType &&
				!string.Equals(rootType, "object", StringComparison.Ordinal))
			{
				// A bare value: the schema describes the argument itself, not a property of an object.
				var bare = ToolArgumentDescriptor.FromRoot(root);

				return new ToolArgumentsDescriptor
				{
					Arguments = [bare],
					MainName = bare.Name,
					IsObject = false,
					RootSchema = root
				};
			}

			var required = root["required"] is JsonArray names
				? names
					.Select(node => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null)
					.OfType<string>()
					.ToHashSet(StringComparer.Ordinal)
				: [];

			var arguments = ImmutableArray.CreateBuilder<ToolArgumentDescriptor>();
			string? mainName = null;

			if (root["properties"] is JsonObject properties)
			{
				foreach (var (name, node) in properties)
				{
					if (node is not JsonObject property)
						continue;

					var argument = ToolArgumentDescriptor.FromJson(name, property, required.Contains(name), arguments.Count);
					arguments.Add(argument);

					if (!argument.IsMain)
						continue;

					if (mainName is not null)
						throw new ArgumentException(
							$"The argument schema marks more than one main argument ('{mainName}' and '{name}').",
							nameof(schema));

					mainName = name;
				}
			}

			return new ToolArgumentsDescriptor
			{
				Arguments = arguments.ToImmutable(),
				MainName = mainName,
				RootSchema = root
			};
		}

		/// <summary>
		/// Finds an argument by the name of the C# parameter it was generated from. Capability methods
		/// use it to bind against the main declaration of the same argument instead of repeating its
		/// name, default and attributes.
		/// </summary>
		/// <param name="clrName">The C# parameter name.</param>
		/// <param name="descriptor">The argument, or <see langword="null"/> when no argument came from such a parameter.</param>
		/// <returns><see langword="true"/> when the argument exists.</returns>
		public bool TryGetClrName(string clrName, [NotNullWhen(true)] out ToolArgumentDescriptor? descriptor)
		{
			foreach (var argument in Arguments)
			{
				if (string.Equals(argument.ClrName, clrName, StringComparison.Ordinal))
				{
					descriptor = argument;
					return true;
				}
			}

			descriptor = null;
			return false;
		}

		/// <summary>
		/// Builds the JSON schema the model sees: the arguments with the metadata already trimmed from
		/// each one. An authored root is returned with its own keys untouched, so nothing the author
		/// wrote around the arguments is lost to the projection. A bare-value tool renders its main
		/// argument's schema as the whole argument schema.
		/// </summary>
		/// <returns>A new schema node, detached from every descriptor it was built from.</returns>
		public JsonObject ToSchema()
		{
			if (!IsObject)
				return Main?.Schema.DeepClone().AsObject() ?? new JsonObject();

			if (RootSchema is { } root)
			{
				var authored = root.DeepClone().AsObject();

				if (authored["properties"] is JsonObject authoredProperties)
				{
					foreach (var argument in Arguments)
						authoredProperties[argument.Name] = argument.Schema.DeepClone();
				}

				return authored;
			}

			var properties = new JsonObject();
			var required = new JsonArray();

			foreach (var argument in Arguments.OrderBy(argument => argument.Order))
			{
				// A JsonNode cannot have two parents, and the same descriptor serves several clones of
				// the tool, so every schema gets its own copy.
				properties[argument.Name] = argument.Schema.DeepClone();
				if (argument.Required)
					required.Add(argument.Name);
			}

			var schema = new JsonObject
			{
				["type"] = "object",
				["properties"] = properties,
				["additionalProperties"] = false
			};

			if (required.Count > 0)
				schema["required"] = required;

			return schema;
		}
	}
}
