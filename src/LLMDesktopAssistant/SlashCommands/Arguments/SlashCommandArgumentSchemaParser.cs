using System.Collections.Immutable;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.StructuredValues;
using LLMDesktopAssistant.StructuredValues.Const;

namespace LLMDesktopAssistant.SlashCommands.Arguments
{
	/// <summary>
	/// Parses a command's <c>argument-schema</c> frontmatter value into a <see cref="SlashCommandArgumentSchema"/>.
	/// </summary>
	/// <remarks>
	/// The value comes in one of two shapes, distinguished by its YAML type:
	/// <list type="bullet">
	///   <item><description>a <em>string</em> — the compact hint parsed by <see cref="ArgumentHintParser"/>;</description></item>
	///   <item><description>a <em>mapping</em> — the structured DSL below.</description></item>
	/// </list>
	/// <code>
	/// argument-schema:
	///   positionals:
	///     - name: query
	///       description: What to search for
	///       required: true
	///   keyed:
	///     wait:
	///       type: boolean
	///       default: "true"
	///   rest:
	///     name: message
	///     description: Free-form tail
	/// </code>
	/// Per-argument fields: <c>name</c> (positionals/rest only; a keyed argument is named by its map key),
	/// <c>description</c>, <c>required</c>, <c>default</c>, <c>type</c>
	/// (<c>string</c>| <c>boolean</c> | <c>number</c> | <c>choice</c>) and <c>choices</c> (for <c>choice</c>).
	/// </remarks>
	public static class SlashCommandArgumentSchemaParser
	{
		/// <summary>
		/// Parses the raw <c>argument-schema</c> node.
		/// </summary>
		/// <param name="node">The raw frontmatter node, or <see langword="null"/> when the key is absent.</param>
		/// <param name="invalid">
		/// Whether the node was present but its shape was not understood (the caller should report
		/// <see cref="LLMDesktopAssistant.Addons.AddonDiagnosticCode.InvalidArgumentHint"/>).
		/// </param>
		/// <returns>The parsed schema, or <see langword="null"/> when the command declares no arguments.</returns>
		public static SlashCommandArgumentSchema? Parse(INodeValue? node, out bool invalid)
		{
			invalid = false;

			switch (node)
			{
				case null or INodeNullValue:
					return null;

				case INodeStringValue hint:
					return ArgumentHintParser.Build(hint.Value, out invalid);

				case INodeDictionaryValue dsl:
					return ParseDsl(dsl, out invalid);

				default:
					invalid = true;
					return null;
			}
		}

		private static SlashCommandArgumentSchema? ParseDsl(INodeDictionaryValue dsl, out bool invalid)
		{
			invalid = false;

			var positionals = ImmutableList.CreateBuilder<SlashCommandArgument>();
			var keyed = ImmutableDictionary.CreateBuilder<string, SlashCommandArgument>();
			SlashCommandArgument? rest = null;

			if (dsl.TryGetValue("positionals", out INodeValue? positionalNode))
			{
				if (positionalNode is not INodeArrayValue array)
				{
					invalid = true;
					return null;
				}

				foreach (var item in array.Items)
				{
					if (item is not INodeDictionaryValue arg ||
						!TryBuildArgument(arg, explicitName: null, out var definition))
					{
						invalid = true;
						return null;
					}

					positionals.Add(definition);
				}
			}

			if (dsl.TryGetValue("keyed", out INodeValue? keyedNode))
			{
				if (keyedNode is not INodeDictionaryValue keyedMap)
				{
					invalid = true;
					return null;
				}

				foreach (var (key, value) in keyedMap.Items)
				{
					if (value is not INodeDictionaryValue arg ||
						!TryBuildArgument(arg, explicitName: key, out var definition))
					{
						invalid = true;
						return null;
					}

					keyed[key] = definition;
				}
			}

			if (dsl.TryGetValue("rest", out INodeValue? restNode))
			{
				if (restNode is not INodeDictionaryValue restArg ||
					!TryBuildArgument(restArg, explicitName: null, out var definition))
				{
					invalid = true;
					return null;
				}

				rest = definition;
			}

			return new SlashCommandArgumentSchema
			{
				Positionals = positionals.ToImmutable(),
				Keyed = keyed.ToImmutable(),
				RestPositional = rest
			};
		}

		private static bool TryBuildArgument(INodeDictionaryValue arg, string? explicitName,
			out SlashCommandArgument definition)
		{
			definition = null!;

			var name = explicitName ??
				(arg.TryGetValue("name", out INodeValue? nameNode) ? nameNode.AsString() : null);
			if (string.IsNullOrWhiteSpace(name))
				return false;

			var description = arg.TryGetValue("description", out INodeValue? descriptionNode)
				? descriptionNode.AsString()
				: null;
			var required = arg.TryGetValue("required", out INodeValue? requiredNode) &&
				requiredNode is INodeBooleanValue { Value: true };
			var defaultValue = arg.TryGetValue("default", out INodeValue? defaultNode)
				? defaultNode.AsString()
				: null;
			var type = (arg.TryGetValue("type", out INodeValue? typeNode) ? typeNode.AsString() : null) ?? "string";

			ISlashCommandArgumentFormatProvider? format = type.Trim().ToLowerInvariant() switch
			{
				"boolean" or "bool" => SlashCommandBooleanFormatProvider.Instance,
				"number" or "float" or "double" => NumberArgumentFormatProvider.Instance,
				"choice" => BuildChoiceProvider(arg),
				_ => null
			};

			definition = new SlashCommandArgument
			{
				Name = Locale.GetConstKey(name),
				Description = string.IsNullOrWhiteSpace(description) ? null : Locale.GetConstKey(description),
				Required = required,
				Default = defaultValue,
				Format = format
			};
			return true;
		}

		private static ISlashCommandArgumentFormatProvider? BuildChoiceProvider(INodeDictionaryValue arg)
		{
			if (!arg.TryGetValue("choices", out INodeValue? choicesNode) || choicesNode is not INodeArrayValue choicesArray)
				return null;

			var choices = choicesArray.Items
				.Select(choice => choice.AsString() ?? string.Empty)
				.Where(choice => choice.Length > 0)
				.ToImmutableList();

			return choices.Count > 0 ? new ChoiceArgumentFormatProvider(choices, strict: true) : null;
		}
	}
}
