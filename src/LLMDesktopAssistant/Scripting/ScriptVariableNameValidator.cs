namespace LLMDesktopAssistant.Scripting
{
	/// <summary>
	/// Checks the names a script's preamble exposes as variables.
	/// </summary>
	/// <remarks>
	/// A name lands in the generated document verbatim: an ampersand in a batch <c>set "name=..."</c> is a command
	/// injection, and a name the preamble uses itself (<c>os</c> in Python) replaces what the body expects.
	/// </remarks>
	public static class ScriptVariableNameValidator
	{
		/// <summary>
		/// Validates one name against the identifier grammar and the language's reserved names.
		/// </summary>
		/// <exception cref="ArgumentException">The name is not an identifier, or is reserved.</exception>
		public static void Validate(string name, string language, IReadOnlySet<string> reserved)
		{
			if (!IsValidIdentifier(name))
			{
				throw new ArgumentException(
					$"The variable name '{name}' is not a valid identifier: use letters, digits and underscores, and do " +
					$"not start with a digit.");
			}

			if (reserved.Contains(name))
			{
				throw new ArgumentException(
					$"The variable name '{name}' is reserved by the {language} preamble; choose another name.");
			}
		}

		/// <summary>
		/// Whether the name matches <c>[A-Za-z_][A-Za-z0-9_]*</c>.
		/// </summary>
		public static bool IsValidIdentifier(string? name)
		{
			if (string.IsNullOrEmpty(name))
				return false;

			if (name[0] is not ('_' or (>= 'A' and <= 'Z') or (>= 'a' and <= 'z')))
				return false;

			for (var i = 1; i < name.Length; i++)
			{
				if (name[i] is not ('_' or (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9')))
					return false;
			}

			return true;
		}
	}
}
