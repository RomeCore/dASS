using System.Diagnostics.CodeAnalysis;
using LLMDesktopAssistant.StructuredValues;
using LLMDesktopAssistant.StructuredValues.Const;

namespace LLMDesktopAssistant.Addons.Parsers.Frontmatter
{
	/// <summary>
	/// Parses an enum frontmatter property, e.g. skill's 'injection-mode' or a tool addon's 'approval-level'.
	/// Supports kebab-case ('read-only'), snake_case ('read_only') and plain enum names case-insensitively.
	/// </summary>
	public class AddonFrontmatterEnumPropertyParser<TEnum> : AddonFrontmatterPropertyParser<TEnum>
		where TEnum : struct, Enum
	{
		public override bool TryParse(ConstNodeValue value, ref AddonDiagnostic? diagnostic, bool require, [NotNullWhen(true)] out TEnum result)
		{
			if (value is ConstNodeStringValue strValue
				&& !string.IsNullOrWhiteSpace(strValue.Value)
				&& AddonFrontmatterEnumParserHelper.TryParseEnum<TEnum>(strValue.Value, out result))
			{
				return true;
			}

			result = default;
			diagnostic = AddonDiagnostic.Combine(diagnostic, new AddonDiagnostic
			{
				IsFatal = require,
				Messages = [$"Expected a valid '{typeof(TEnum).Name}' value"]
			});
			return false;
		}
	}
}
