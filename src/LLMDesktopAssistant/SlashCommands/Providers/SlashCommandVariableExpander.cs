using System.Text;
using LLMDesktopAssistant.Prompting.Skills;
using LLMDesktopAssistant.SlashCommands.Arguments;

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
		/// <summary>The command's rest-positional text, exposed as <c>$ARGUMENTS</c>.</summary>
		public const string ArgumentsVariable = "ARGUMENTS";

		/// <summary>The skill's home directory, exposed as <c>$CLAUDE_SKILL_DIR</c>.</summary>
		public const string SkillDirectoryVariable = "CLAUDE_SKILL_DIR";

		/// <summary>A short alias of <see cref="SkillDirectoryVariable"/>: <c>$SKILL_DIR</c>.</summary>
		public const string SkillDirectoryAliasVariable = "SKILL_DIR";

		/// <summary>The skill's name, exposed as <c>$SKILL_NAME</c>.</summary>
		public const string SkillNameVariable = "SKILL_NAME";

		/// <summary>
		/// Resolves a single variable, or <see langword="null"/> when it is unknown (the caller then leaves it
		/// verbatim).
		/// </summary>
		/// <param name="name">The variable name, without the leading <c>$</c>.</param>
		/// <param name="arguments">The command's bound arguments.</param>
		/// <param name="skill">The skill the body belongs to; provides the skill-scoped variables.</param>
		public static string? GetSkillVariable(string name, SlashCommandBoundArguments arguments, SkillInfo skill)
		{
			if (string.Equals(name, ArgumentsVariable, StringComparison.OrdinalIgnoreCase))
				return arguments.RestPositionalArguments;

			if (string.Equals(name, SkillNameVariable, StringComparison.OrdinalIgnoreCase))
				return skill.Name;

			if (string.Equals(name, SkillDirectoryVariable, StringComparison.OrdinalIgnoreCase) ||
				string.Equals(name, SkillDirectoryAliasVariable, StringComparison.OrdinalIgnoreCase))
				return GetSkillDirectory(skill);

			return Environment.GetEnvironmentVariable(name);
		}

		/// <summary>
		/// Expands every <c>$NAME</c> / <c>${NAME}</c> variable in <paramref name="body"/>. Unknown variables are left
		/// verbatim.
		/// </summary>
		public static string ExpandSkill(string body, SlashCommandBoundArguments arguments, SkillInfo skill)
		{
			if (string.IsNullOrEmpty(body) || !body.Contains('$'))
				return body;

			var builder = new StringBuilder(body.Length);
			for (int i = 0; i < body.Length; i++)
			{
				if (body[i] != '$' || i + 1 >= body.Length)
				{
					builder.Append(body[i]);
					continue;
				}

				int nameStart = i + 1;
				string name;
				int nameEnd; // index of the last character of the whole variable occurrence

				if (body[nameStart] == '{')
				{
					int close = body.IndexOf('}', nameStart + 1);
					if (close < 0)
					{
						builder.Append(body[i]);
						continue;
					}

					name = body[(nameStart + 1)..close];
					nameEnd = close;
				}
				else
				{
					int j = nameStart;
					while (j < body.Length && (char.IsLetterOrDigit(body[j]) || body[j] == '_'))
						j++;

					if (j == nameStart)
					{
						builder.Append(body[i]);
						continue;
					}

					name = body[nameStart..j];
					nameEnd = j - 1;
				}

				if (!IsValidName(name))
				{
					builder.Append(body[i]);
					continue;
				}

				var value = GetSkillVariable(name, arguments, skill);
				if (value is null)
				{
					// Unknown variable: keep the occurrence exactly as written.
					builder.Append(body, i, nameEnd - i + 1);
				}
				else
				{
					builder.Append(value);
				}

				i = nameEnd;
			}

			return builder.ToString();
		}

		private static bool IsValidName(string name)
			=> name.Length > 0 && (char.IsLetter(name[0]) || name[0] == '_');

		private static string? GetSkillDirectory(SkillInfo skill)
			=> skill.HomeDirectory is { Length: > 0 } home
				? home
				: skill.Path is { Length: > 0 } path
					? Path.GetDirectoryName(path)
					: null;
	}
}
