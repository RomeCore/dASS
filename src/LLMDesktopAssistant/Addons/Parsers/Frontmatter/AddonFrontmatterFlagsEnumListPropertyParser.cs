using System.Diagnostics.CodeAnalysis;
using LLMDesktopAssistant.StructuredValues;
using LLMDesktopAssistant.StructuredValues.Const;

namespace LLMDesktopAssistant.Addons.Parsers.Frontmatter
{
	/// <summary>
	/// Parses a flags enum list frontmatter property, e.g. a tool addon's 'behaviours'.
	/// Accepts an array of flag names ('file-read', 'internet-access'), a comma-separated scalar,
	/// or a single flag name. Flags are combined with a bitwise OR.
	/// </summary>
	public class AddonFrontmatterFlagsEnumListPropertyParser<TEnum> : AddonFrontmatterPropertyParser<TEnum>
		where TEnum : struct, Enum
	{
		public override bool TryParse(ConstNodeValue value, ref AddonDiagnostic? diagnostic, bool require, [NotNullWhen(true)] out TEnum result)
		{
			var flagsValue = 0L;

			switch (value)
			{
				case ConstNodeArrayValue arrayValue:
					foreach (var item in arrayValue.Items)
					{
						var str = item.AsString();
						if (!string.IsNullOrWhiteSpace(str))
						{
							if (!AddonFrontmatterEnumParserHelper.TryParseFlags<TEnum>(str, out var flag))
							{
								result = default;
								diagnostic = AddonDiagnostic.Combine(diagnostic, new AddonDiagnostic
								{
									IsFatal = require,
									Messages = [$"Expected a valid '{typeof(TEnum).Name}' flag"]
								});
								return false;
							}
							flagsValue |= Convert.ToInt64(flag, System.Globalization.CultureInfo.InvariantCulture);
						}
					}
					result = (TEnum)Enum.ToObject(typeof(TEnum), flagsValue);
					return true;

				case ConstNodeStringValue strValue when !string.IsNullOrWhiteSpace(strValue.Value):
					if (AddonFrontmatterEnumParserHelper.TryParseFlags<TEnum>(strValue.Value, out var singleFlag))
					{
						result = singleFlag;
						return true;
					}
					break;
			}

			result = default;
			diagnostic = AddonDiagnostic.Combine(diagnostic, new AddonDiagnostic
			{
				IsFatal = require,
				Messages = [$"Expected a valid '{typeof(TEnum).Name}' flag or a list of flags"]
			});
			return false;
		}
	}
}
