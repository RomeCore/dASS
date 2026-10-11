using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Prompting.Skills;
using LLMDesktopAssistant.SlashCommands.Arguments;
using RCParsing;

namespace LLMDesktopAssistant.SlashCommands.Providers
{
	/// <summary>
	/// Expands the <c>$NAME</c> / <c>${NAME}</c> variables of a skill body before it is injected into a message.
	/// </summary>
	/// <remarks>
	/// The built-ins are the command's own arguments (<c>$ARGUMENTS</c>) and the skill's identity
	/// (<c>$CLAUDE_SKILL_DIR</c> / <c>$SKILL_DIR</c>, <c>$SKILL_NAME</c>); anything else resolves against the process
	/// environment. An unknown variable is left <em>verbatim</em> — a typo must not silently eat text. All the
	/// expansion logic lives here, so the executor only calls <see cref="ExpandSkill"/>.
	/// </remarks>
	public static class SlashCommandVariableExpander
	{
		private static readonly Parser _replacer;

		private static bool IsValidNameChar(char c)
		{
			return char.IsLetterOrDigit(c) || c == '_' || c == '-';
		}

		static SlashCommandVariableExpander()
		{
			var builder = new ParserBuilder();

			builder.CreateRule("name")
				.Choice(
					b => b.IntegerNumber<int>()
						.Transform(v =>
						{
							var index = v.GetIntermediateValue<int>();
							var (isZeroBased, arguments, skill) = v.GetParsingParameter<(bool, SlashCommandBoundArguments, SkillInfo)>();
							if (!isZeroBased)
								index--; // Convert to zero-based index
							return GetSkillVariable(index, arguments, skill);
						}),
					b => b.OneOrMoreChars(IsValidNameChar)
						.Transform(v =>
						{
							var name = v.Text;
							var (isZeroBased, arguments, skill) = v.GetParsingParameter<(bool, SlashCommandBoundArguments, SkillInfo)>();
							return GetSkillVariable(name, arguments, skill);
						})
				);

			builder.CreateRule("substitution")
				.Choice(
					b => b.Literal('`').TextUntil('`').Literal('`'),
					b => b.Literal("```").TextUntil("```").Literal("```")
				)

				.Transform(v => v.Text);

			builder.CreateRule("variable")
				.Literal('$')

				.Choice(
					b => b
						.Literal('{')
						.Rule("name").Label("name")
						.Literal('}')

						.Transform(v => v["name"].Value),
					b => b.Rule("name")
				).Label("variable")

				.Transform(v =>
				{
					var value = v.TryGetValue<string?>("variable");

					if (value is null)
						return v.Text; // Pass verbatim if no match found.

					return value;
				});

			builder.CreateMainRule()
				.Choice(
					b => b.Rule("substitution"),
					b => b.Rule("variable")
				);

			_replacer = builder.Build();
		}

		/// <summary>The command's raw positional text, exposed as <c>$ARGUMENTS</c>.</summary>
		public const string ArgumentsVariable = "ARGUMENTS";

		/// <summary>The command's rest positional arguments text, exposed as <c>REST</c>.</summary>
		public const string RestPositionalArgumentsVariable = "REST";

		/// <summary>The skill's home directory, exposed as <c>$CLAUDE_SKILL_DIR</c>.</summary>
		public const string SkillDirectoryVariable = "CLAUDE_SKILL_DIR";

		/// <summary>A short alias of <see cref="SkillDirectoryVariable"/>: <c>$SKILL_DIR</c>.</summary>
		public const string SkillDirectoryAliasVariable = "SKILL_DIR";

		/// <summary>The skill's name, exposed as <c>$SKILL_NAME</c>.</summary>
		public const string SkillNameVariable = "SKILL_NAME";

		/// <summary>
		/// Resolves a single positional variable, or <see langword="null"/> when it is unknown (the caller then leaves it
		/// verbatim).
		/// </summary>
		/// <param name="index">The variable index, 0-based.</param>
		/// <param name="arguments">The command's bound arguments.</param>
		/// <param name="skill">The skill the body belongs to; provides the skill-scoped variables.</param>
		public static string? GetSkillVariable(int index, SlashCommandBoundArguments arguments, SkillInfo skill)
		{
			if (index >= 0 && index < arguments.Positionals.Count)
				return arguments.Positionals[index].Ready ?? string.Empty;

			return null;
		}

		/// <summary>
		/// Resolves a single variable, or <see langword="null"/> when it is unknown (the caller then leaves it
		/// verbatim).
		/// </summary>
		/// <param name="name">The variable name, without the leading <c>$</c>.</param>
		/// <param name="arguments">The command's bound arguments.</param>
		/// <param name="skill">The skill the body belongs to; provides the skill-scoped variables.</param>
		public static string? GetSkillVariable(string name, SlashCommandBoundArguments arguments, SkillInfo skill)
		{
			if (skill.ArgumentSchema is { } schema)
			{
				if (schema.RestPositional is { } rest && rest.Name is ConstLocaleKey { } restArgName && restArgName.Key == name)
					return arguments.RestPositionalArguments;

				for (int i = 0; i < schema.Positionals.Count; i++)
				{
					var positional = schema.Positionals[i];
					// Schema's positional argument names are created from 'argument-hint' names
					if (positional.Name is ConstLocaleKey { } argName && argName.Key == name)
						return arguments.Positionals[i].Ready ?? string.Empty;
				}
			}

			if (string.Equals(name, ArgumentsVariable, StringComparison.OrdinalIgnoreCase))
				return arguments.RawPositionalArguments;

			if (string.Equals(name, RestPositionalArgumentsVariable, StringComparison.OrdinalIgnoreCase))
				return arguments.RestPositionalArguments;

			if (string.Equals(name, SkillNameVariable, StringComparison.OrdinalIgnoreCase))
				return skill.Name;

			if (string.Equals(name, SkillDirectoryVariable, StringComparison.OrdinalIgnoreCase) ||
				string.Equals(name, SkillDirectoryAliasVariable, StringComparison.OrdinalIgnoreCase))
				return skill.HomeDirectory;

			return Environment.GetEnvironmentVariable(name);
		}

		/// <summary>
		/// Expands every <c>$NAME</c> / <c>${NAME}</c> variable in <paramref name="body"/>. Unknown variables are left
		/// verbatim.
		/// </summary>
		public static string ExpandSkill(string body, SlashCommandBoundArguments arguments, SkillInfo skill)
		{
			bool isZeroBased = body.Contains("$0");
			return _replacer.ReplaceAllMatches(body, parameter: (isZeroBased, arguments, skill));
		}
	}
}
