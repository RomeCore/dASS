namespace LLMDesktopAssistant.Providers
{
	/// <summary>
	/// Represents a parsed model full name in format "Provider$Model" or "Provider$Model$Modifier".
	/// </summary>
	public readonly record struct ModelReference(string Provider, string ModelId, IReadOnlyList<string>? Modifiers)
	{
		/// <summary>
		/// Parses a model full name.
		/// </summary>
		/// <param name="fullName">The full name of the model in format "Provider$Model" or "Provider$Model$Modifier".</param>
		/// <returns>The parsed model reference.</returns>
		/// <exception cref="ArgumentException">Thrown when the full name format is invalid.</exception>
		public static ModelReference Parse(string fullName)
		{
			if (!TryParse(fullName, out var reference))
				throw new ArgumentException(
					"Invalid model full name format. Expected \"Provider$Model\" or \"Provider$Model$Modifier\".",
					nameof(fullName));
			return reference;
		}

		/// <summary>
		/// Tries to parse a model full name.
		/// </summary>
		/// <param name="fullName">The full name of the model in format "Provider$Model" or "Provider$Model$Modifier".</param>
		/// <param name="reference">When this method returns <see langword="true"/>, contains the parsed model reference.</param>
		/// <returns><see langword="true"/> if the full name was parsed successfully, otherwise <see langword="false"/>.</returns>
		public static bool TryParse(string fullName, out ModelReference reference)
		{
			reference = default;
			if (string.IsNullOrWhiteSpace(fullName))
				return false;

			var parts = fullName.Split('$');
			if (parts.Length is < 2 or > 3)
				return false;
			if (string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
				return false;
			if (parts.Length == 3 && string.IsNullOrWhiteSpace(parts[2]))
				return false;

			if (parts.Length == 2)
			{
				reference = new ModelReference(parts[0], parts[1], []);
				return true;
			}

			reference = new ModelReference(parts[0], parts[1], [.. parts[2].Split(',')]);
			return true;
		}

		/// <summary>
		/// Returns the full name in format "Provider$Model" or "Provider$Model$Mod1,Mod2,...".
		/// </summary>
		/// <returns>The full name of the model.</returns>
		public override string ToString()
			=> (Modifiers is null || Modifiers.Count == 0)
				? $"{Provider}${ModelId}"
				: $"{Provider}${ModelId}${string.Join(',', Modifiers)}";
	}
}
