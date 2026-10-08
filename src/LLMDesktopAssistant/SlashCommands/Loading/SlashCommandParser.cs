using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Addons.Parsers;
using LLMDesktopAssistant.Addons.Parsers.Frontmatter;
using LLMDesktopAssistant.Services;

namespace LLMDesktopAssistant.SlashCommands.Loading
{
	/// <summary>
	/// The parser of file-based slash commands.
	/// </summary>
	/// <remarks>
	/// Inert in v1 — it exists only so the addon pipeline can construct the command loader. File commands (frontmatter
	/// plus a body, with frontmatter fields <c>namespaces</c>, <c>model-facing</c>, <c>generate</c> and
	/// <c>argument-schema</c>) are a v2 concern and will fill in <see cref="Populate"/>.
	/// </remarks>
	[Service(typeof(IAddonFileParser<SlashCommandInfo>))]
	public class SlashCommandParser : FrontmatterBasedAddonParser<SlashCommandInfo>
	{
		protected override AddonParserDescriptor GetDescriptorFor(string content, AddonPathInfo fileInfo)
		{
			return new AddonParserDescriptor
			{
				FrontmatterStart = "---",
				FrontmatterEnd = "---"
			};
		}

		protected override void Populate(SlashCommandInfo addon, AddonFrontmatterDocument frontmatter,
			ref AddonDiagnostic? diagnostic)
		{
		}
	}
}
