using System.Collections.Immutable;
using LLMDesktopAssistant.Localization;
using RCParsing;

namespace LLMDesktopAssistant.SlashCommands.Arguments
{
	/// <summary>
	/// Parses a skill's <c>argument-hint</c> frontmatter string into a <see cref="SlashCommandArgumentSchema"/>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A hint is a list of bracketed arguments, whitespace between them optional (a bracket pair is already an
	/// argument): <c>&lt;name&gt;</c> is a required positional and <c>[name]</c> an optional one. Inside the brackets
	/// the syntax extends to <c>[name...:$v1|v2:%description]</c> — a trailing <c>...</c> makes the argument the rest
	/// positional, <c>$a|b</c> lists the accepted values (offered as completions and, when strict, validated) and
	/// <c>%…</c> is the human-readable description. The reserved characters are escaped with a backslash
	/// (<c>\]</c>, <c>\&gt;</c>, <c>\|</c>, <c>\:</c>, <c>\%</c>, <c>\$</c>, <c>\\</c>). A required rest positional
	/// (<c>&lt;name...&gt;</c>) and a second rest positional are rejected.
	/// </para>
	/// <para>
	/// <see cref="Build"/> never throws. An empty hint yields the default free-form <c>arguments</c> rest slot; a hint
	/// that does not start with <c>&lt;</c> or <c>[</c> is free-form text and likewise yields a rest slot whose
	/// description is the raw hint (with no diagnostic); a hint that starts with a bracket but fails to parse yields
	/// that same fallback and reports <paramref name="invalid"/> so the caller can raise
	/// <see cref="LLMDesktopAssistant.Addons.AddonDiagnosticCode.InvalidArgumentHint"/>.
	/// </para>
	/// </remarks>
	public static class SkillArgumentHintParser
	{
		/// <summary>The locale key of the default free-form arguments slot's name.</summary>
		private const string ArgumentsKey = "command.argument.skill.arguments";

		/// <summary>The locale key of the default free-form arguments slot's description.</summary>
		private const string ArgumentsDescriptionKey = "command.argument.skill.arguments.description";

		private static readonly Parser _parser = BuildParser();

		/// <summary>
		/// Builds the argument schema of a skill from its raw <c>argument-hint</c>.
		/// </summary>
		/// <param name="hint">The raw hint, or <see langword="null"/>/whitespace when the skill declares none.</param>
		/// <param name="invalid">
		/// Whether the hint started with <c>&lt;</c> or <c>[</c> but could not be parsed — the caller should report
		/// <see cref="LLMDesktopAssistant.Addons.AddonDiagnosticCode.InvalidArgumentHint"/>.
		/// </param>
		/// <returns>A non-null schema: the parsed arguments or the free-form rest-slot fallback.</returns>
		public static SlashCommandArgumentSchema Build(string? hint, out bool invalid)
		{
			invalid = false;

			if (string.IsNullOrWhiteSpace(hint))
				return RestSchema(Locale.GetKey(ArgumentsKey), Locale.GetKey(ArgumentsDescriptionKey));

			hint = hint.Trim();
			var startsWithArgument = hint[0] is '<' or '[';

			ImmutableList<HintArgument>? parsed = null;
			if (startsWithArgument && _parser.TryParse(hint, out ImmutableList<HintArgument> result))
				parsed = result;

			if (parsed is { Count: > 0 })
			{
				var schema = TryBuildSchema(parsed);
				if (schema is not null)
					return schema;
			}

			invalid = startsWithArgument;
			return RestSchema(Locale.GetKey(ArgumentsKey), Locale.GetConstKey(hint));
		}

		private static SlashCommandArgumentSchema RestSchema(LocaleKeyBase name, LocaleKeyBase description)
			=> new()
			{
				RestPositional = new SlashCommandArgument
				{
					Name = name,
					Description = description
				}
			};

		private static SlashCommandArgumentSchema? TryBuildSchema(ImmutableList<HintArgument> arguments)
		{
			var positionals = ImmutableList.CreateBuilder<SlashCommandArgument>();
			SlashCommandArgument? rest = null;

			foreach (var argument in arguments)
			{
				string? name = null;
				string? description = null;
				ImmutableList<string> choices = [];
				var isRest = false;
				var hasName = false;
				var hasRest = false;
				var hasChoices = false;
				var hasDescription = false;

				foreach (var part in argument.Parts)
				{
					switch (part.Kind)
					{
						case HintPartKind.Name:
							if (hasName)
								return null;
							hasName = true;
							name = part.Text;
							break;

						case HintPartKind.Rest:
							if (hasRest)
								return null;
							hasRest = true;
							isRest = true;
							break;

						case HintPartKind.Choices:
							if (hasChoices)
								return null;
							hasChoices = true;
							choices = part.Choices;
							break;

						case HintPartKind.Description:
							if (hasDescription)
								return null;
							hasDescription = true;
							description = part.Text;
							break;
					}
				}

				// The name is mandatory and must come first; a required rest positional is not a thing.
				if (!hasName || string.IsNullOrWhiteSpace(name))
					return null;
				if (argument.Parts.Count == 0 || argument.Parts[0].Kind != HintPartKind.Name)
					return null;
				if (isRest && argument.Required)
					return null;

				var definition = new SlashCommandArgument
				{
					Name = Locale.GetConstKey(name),
					Description = string.IsNullOrWhiteSpace(description) ? null : Locale.GetConstKey(description),
					Required = argument.Required && !isRest,
					Default = null,
					Format = choices.Count > 0 ? new ChoiceArgumentFormatProvider(choices, strict: true) : null
				};

				if (isRest)
				{
					if (rest is not null)
						return null;
					rest = definition;
				}
				else
				{
					positionals.Add(definition);
				}
			}

			return new SlashCommandArgumentSchema
			{
				Positionals = positionals.ToImmutable(),
				RestPositional = rest
			};
		}

		private static bool IsNameChar(char c)
			=> char.IsLetterOrDigit(c) || c == '_' || c == '-';

		private static Parser BuildParser()
		{
			var escapes = new KeyValuePair<string, string>[]
			{
				KeyValuePair.Create("\\\\", "\\"),
				KeyValuePair.Create("\\]", "]"),
				KeyValuePair.Create("\\>", ">"),
				KeyValuePair.Create("\\|", "|"),
				KeyValuePair.Create("\\:", ":"),
				KeyValuePair.Create("\\%", "%"),
				KeyValuePair.Create("\\$", "$")
			};

			var builder = new ParserBuilder();
			builder.Settings.SkipWhitespaces();

			builder.CreateToken("name")
				.OneOrMoreChars(IsNameChar);

			builder.CreateToken("choice")
				.EscapedText(escapes, [":", "|", "]", ">"], allowsEmpty: false)
				.Transform(v => v.GetIntermediateValue<string>());

			builder.CreateToken("description")
				.EscapedText(escapes, ["]", ">"])
				.Transform(v => v.GetIntermediateValue<string>());

			builder.CreateRule("part_name")
				.Token("name")
				.Transform(v => new HintPart { Kind = HintPartKind.Name, Text = v.Text });

			builder.CreateRule("part_rest")
				.Literal("...")
				.Transform(_ => new HintPart { Kind = HintPartKind.Rest });

			builder.CreateRule("part_choices")
				.Literal(':')
				.Literal('$')
				.OneOrMoreSeparated(b => b.Token("choice"), b => b.Literal('|'), includeSeparatorsInResult: false)
				.Transform(v => new HintPart
				{
					Kind = HintPartKind.Choices,
					Choices = v[2].SelectValues<string>().ToImmutableList()
				});

			builder.CreateRule("part_description")
				.Literal(':')
				.Literal('%')
				.Token("description")
				.Transform(v => new HintPart { Kind = HintPartKind.Description, Text = v.GetValue<string>(2) });

			builder.CreateRule("body")
				.OneOrMore(b => b.Choice(
					c => c.Rule("part_name"),
					c => c.Rule("part_rest"),
					c => c.Rule("part_choices"),
					c => c.Rule("part_description")))
				.Transform(v => v.SelectValues<HintPart>().ToImmutableList());

			builder.CreateRule("required")
				.Literal('<')
				.Rule("body")
				.Literal('>')
				.Transform(v => new HintArgument
				{
					Parts = v[1].GetValue<ImmutableList<HintPart>>(),
					Required = true
				});

			builder.CreateRule("optional")
				.Literal('[')
				.Rule("body")
				.Literal(']')
				.Transform(v => new HintArgument
				{
					Parts = v[1].GetValue<ImmutableList<HintPart>>(),
					Required = false
				});

			builder.CreateMainRule()
				.OneOrMore(b => b.Choice(
					c => c.Rule("required"),
					c => c.Rule("optional")))
				.EOF()
				.Transform(v => v[0].SelectValues<HintArgument>().ToImmutableList());

			return builder.Build();
		}

		private enum HintPartKind
		{
			Name,
			Rest,
			Choices,
			Description
		}

		private sealed class HintPart
		{
			public HintPartKind Kind { get; init; }
			public string? Text { get; init; }
			public ImmutableList<string> Choices { get; init; } = [];
		}

		private sealed class HintArgument
		{
			public ImmutableList<HintPart> Parts { get; init; } = [];
			public bool Required { get; init; }
		}
	}
}
