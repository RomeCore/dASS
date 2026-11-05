using System.Collections.Immutable;
using System.Text.Json.Nodes;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.SlashCommands.Arguments;
using LLMDesktopAssistant.Tools.Execution.Domain;

namespace LLMDesktopAssistant.SlashCommands.Providers
{
	/// <summary>
	/// Maps a tool's arguments onto the slash-command argument schema.
	/// </summary>
	/// <remarks>
	/// The main argument becomes the <em>rest</em> positional: it is one verbatim value, so
	/// <c>/tool:shell-bash git status --short</c> reaches the tool as one string instead of forcing
	/// <c>command=…</c> with its quoting rules. Every other argument is keyed, so nothing is inferred
	/// from the schema's property order. The declared <see cref="ToolArgumentKind"/> picks the format
	/// provider, and a closed <c>enum</c> wins over it: it is the only shape that carries a list of
	/// allowed values.
	/// </remarks>
	public static class ToolArgumentSchemaMapper
	{
		/// <summary>
		/// Builds the command argument schema for a tool.
		/// </summary>
		/// <param name="arguments">The arguments of the tool.</param>
		/// <param name="services">
		/// The services a path argument is completed against (its working directory). Required: a path
		/// argument without them would be silently uncompletable.
		/// </param>
		/// <returns>A schema with the main argument as the rest positional and the rest as keyed arguments.</returns>
		public static SlashCommandArgumentSchema Create(ToolArgumentsDescriptor arguments,
			IServiceProvider services)
		{
			ArgumentNullException.ThrowIfNull(arguments);

			if (arguments.Arguments.IsEmpty)
				return new SlashCommandArgumentSchema();

			var builder = ImmutableDictionary.CreateBuilder<string, SlashCommandArgument>(StringComparer.Ordinal);
			SlashCommandArgument? rest = null;

			foreach (var argument in arguments.Arguments.OrderBy(argument => argument.Order))
			{
				if (argument.IsMain)
				{
					rest = ToArgument(argument, services);
					continue;
				}

				builder[argument.Name] = ToArgument(argument, services);
			}

			return new SlashCommandArgumentSchema
			{
				Keyed = builder.ToImmutable(),
				RestPositional = rest
			};
		}

		/// <summary>
		/// Builds the command argument of one tool argument: the name, the description, the required flag, the
		/// authored default and the format provider of its kind.
		/// </summary>
		private static SlashCommandArgument ToArgument(ToolArgumentDescriptor argument,
			IServiceProvider services) => new()
		{
			// A bare-value argument has no JSON name: the label is for the palette, not for a lookup.
			Name = argument.Name is { Length: > 0 }
				? Locale.GetConstKey(argument.Name)
				: Locale.GetKey("command.argument.value"),
			Description = argument.Description is { Length: > 0 } description
				? Locale.GetConstKey(description)
				: null,
			Required = argument.Required,
			Default = DefaultOf(argument),
			Format = FormatOf(argument, services)
		};

		/// <summary>
		/// Reads the authored default. A string default is taken verbatim (it is a value, not a literal);
		/// every other form keeps its JSON text, which is exactly what its format provider expects of a
		/// raw value.
		/// </summary>
		private static string? DefaultOf(ToolArgumentDescriptor argument) => argument.Default switch
		{
			null => null,
			JsonValue value when value.TryGetValue<string>(out var text) => text,
			{ } node => node.ToJsonString()
		};

		/// <summary>
		/// Picks the format provider of an argument, or <see langword="null"/> when the value stays a plain,
		/// unvalidated string. An array carries its choices on the items rather than on itself.
		/// </summary>
		private static ISlashCommandArgumentFormatProvider? FormatOf(ToolArgumentDescriptor argument,
			IServiceProvider services)
		{
			if (argument.Kind is ToolArgumentKind.FilePath or ToolArgumentKind.DirectoryPath or ToolArgumentKind.FileOrDirectory)
			{
				return new FileArgumentFormatProvider(services,
					onlyDirectories: argument.Kind is ToolArgumentKind.DirectoryPath);
			}

			var owner = argument.Kind == ToolArgumentKind.Array
				? argument.Schema["items"] as JsonObject
				: argument.Schema;

			if (owner?["enum"] is JsonArray { Count: > 0 } choices &&
				argument.Kind is ToolArgumentKind.String or ToolArgumentKind.Unspecified)
			{
				return new ChoiceArgumentFormatProvider(choices.Select(TextOf), strict: true);
			}

			return argument.Kind switch
			{
				ToolArgumentKind.Bool => SlashCommandBooleanFormatProvider.Instance,
				ToolArgumentKind.Number or ToolArgumentKind.Integer => NumberArgumentFormatProvider.Instance,
				ToolArgumentKind.Array or ToolArgumentKind.Object => JsonArgumentFormatProvider.Instance,
				_ => null
			};
		}

		/// <summary>
		/// Reads a JSON scalar as text: a string keeps its value, anything else keeps its JSON text.
		/// </summary>
		private static string TextOf(JsonNode? node) =>
			node is JsonValue value && value.TryGetValue<string>(out var text)
				? text
				: node?.ToJsonString() ?? string.Empty;
	}
}
