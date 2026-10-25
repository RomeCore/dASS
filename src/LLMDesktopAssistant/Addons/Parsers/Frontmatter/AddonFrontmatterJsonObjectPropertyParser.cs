using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using LLMDesktopAssistant.StructuredValues.Const;

namespace LLMDesktopAssistant.Addons.Parsers.Frontmatter
{
	/// <summary>
	/// Parses a JSON schema frontmatter property, e.g. a tool addon's 'argument-schema'.
	/// Expects a scalar string containing JSON; produces a <see cref="JsonObject"/>.
	/// </summary>
	public class AddonFrontmatterJsonObjectPropertyParser : AddonFrontmatterPropertyParser<JsonObject>
	{
		public static AddonFrontmatterJsonObjectPropertyParser Instance { get; } = new();

		public override bool TryParse(ConstNodeValue value, ref AddonDiagnostic? diagnostic, bool require, [NotNullWhen(true)] out JsonObject result)
		{
			if (value is ConstNodeStringValue strValue && !string.IsNullOrWhiteSpace(strValue.Value))
			{
				try
				{
					result = (JsonObject?)JsonSerializer.Deserialize<JsonObject>(strValue.Value) ?? new JsonObject();
					return true;
				}
				catch (JsonException ex)
				{
					result = new JsonObject();
					diagnostic = AddonDiagnostic.Combine(diagnostic, new AddonDiagnostic
					{
						IsFatal = require,
						Messages = ["Value is not a valid JSON object"],
						Exceptions = [ex]
					});
					return false;
				}
			}

			result = new JsonObject();
			diagnostic = AddonDiagnostic.Combine(diagnostic, new AddonDiagnostic
			{
				IsFatal = require,
				Messages = ["Expected a JSON string"]
			});
			return false;
		}
	}
}
