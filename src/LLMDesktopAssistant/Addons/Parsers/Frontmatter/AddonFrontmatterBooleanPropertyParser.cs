using System.Diagnostics.CodeAnalysis;
using LLMDesktopAssistant.StructuredValues.Const;

namespace LLMDesktopAssistant.Addons.Parsers.Frontmatter
{
	/// <summary>
	/// Parses a boolean frontmatter property, e.g. a tool addon's 'enabled' field.
	/// Accepts boolean nodes, numeric nodes (zero = false) and string nodes ('true'/'false').
	/// </summary>
	public class AddonFrontmatterBooleanPropertyParser : AddonFrontmatterPropertyParser<bool>
	{
		public static AddonFrontmatterBooleanPropertyParser Instance { get; } = new();

		public override bool TryParse(ConstNodeValue value, ref AddonDiagnostic? diagnostic, bool require, [NotNullWhen(true)] out bool result)
		{
			switch (value)
			{
				case ConstNodeBooleanValue boolValue:
					result = boolValue.Value;
					return true;

				case ConstNodeNumberValue numValue:
					result = numValue.Value != 0;
					return true;

				case ConstNodeStringValue strValue when bool.TryParse(strValue.Value, out var parsed):
					result = parsed;
					return true;

				default:
					result = false;
					diagnostic = AddonDiagnostic.Combine(diagnostic, new AddonDiagnostic
					{
						IsFatal = require,
						Messages = ["Expected a boolean value"]
					});
					return false;
			}
		}
	}
}
