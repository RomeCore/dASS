using System.Text;

namespace LLMDesktopAssistant.Utils.Patterns
{
	/// <summary>
	/// Translates a character class into the regular expression equivalent, validating the ranges
	/// on the way. Shared by both dialects, so a class means the same thing in a path and in a name.
	/// </summary>
	public static class GlobClassCompiler
	{
		/// <summary>
		/// Compiles one class, or throws when a range is reversed.
		/// </summary>
		/// <param name="unit">The class to compile.</param>
		/// <param name="pattern">The source pattern, used in the error message.</param>
		/// <returns>The regular expression fragment the class stands for.</returns>
		public static string Compile(GlobClass unit, string pattern)
		{
			var body = unit.Body;
			var negated = body.Length > 0 && (body[0] == '!' || body[0] == '^');
			var content = negated ? body[1..] : body;

			var builder = new StringBuilder("[");
			if (negated)
				builder.Append('^');

			for (var i = 0; i < content.Length; i++)
			{
				var character = content[i];

				if (character == '-' && i > 0 && i < content.Length - 1)
				{
					var lower = content[i - 1];
					var upper = content[i + 1];

					if (upper < lower)
						throw new GlobException(GlobError.InvalidClassRange, pattern, unit.Position,
							$"Invalid character range '{lower}-{upper}'.");

					builder.Append('-');
					continue;
				}

				if (character is '\\' or ']' or '^' or '[')
					builder.Append('\\');

				builder.Append(character);
			}

			builder.Append(']');
			return builder.ToString();
		}
	}
}
