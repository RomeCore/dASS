using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Addons.Parsers;
using LLMDesktopAssistant.Addons.Parsers.Frontmatter;
using LLMDesktopAssistant.Agents.Settings;
using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.Tools;
using LLMDesktopAssistant.StructuredValues.Parameterization;

namespace LLMDesktopAssistant.Agents.SubAgents
{
	/// <summary>
	/// The parser for sub-agent addon files (<c>agent.md</c>, <c>AGENT.md</c>, <c>*.mdx</c>).
	/// </summary>
	[Service(typeof(IAddonFileParser<SubAgentInfo>))]
	public class SubAgentParser(IParameterSchemaParserManager paramsSchemaParser)
		: FrontmatterBasedAddonParser<SubAgentInfo, SubAgentChange>(paramsSchemaParser)
	{
		protected override AddonParserDescriptor GetDescriptorFor(string content, AddonPathInfo fileInfo)
		{
			return new AddonParserDescriptor
			{
				FrontmatterStart = "---",
				FrontmatterEnd = "---",
				RequiresFrontmatter = false,
				UseMarkdownFallback = true
			};
		}

		protected override void Populate(SubAgentInfo addon, AddonFrontmatterDocument frontmatter, ref AddonDiagnostic? diagnostic)
		{
			base.Populate(addon, frontmatter, ref diagnostic);

			addon.AllowedTools = frontmatter.Get<ImmutableList<ToolNameWithSpecifier>>("allowed-tools", []);
			addon.AvailableTools = frontmatter.Get<ImmutableList<ToolNameWithSpecifier>>("available-tools", []);
			addon.DisallowedTools = frontmatter.Get<ImmutableList<ToolNameWithSpecifier>>("disallowed-tools", []);

			addon.Skills = frontmatter.Get<ImmutableList<string>>("skills", []);
			addon.SubAgents = frontmatter.Get<ImmutableList<string>>("sub-agents", []);

			addon.MemoryBlocks = frontmatter.Get("memory-blocks",
				ImmutableDictionary<string, MemoryBlockAttachmentMode>.Empty,
				new AddonFrontmatterEnumMapPropertyParser<MemoryBlockAttachmentMode>());

			var model = frontmatter.Get<string>("model");
			addon.Model = string.IsNullOrWhiteSpace(model) ? null : model.Trim();
		}

		protected override bool IsValidName(string name)
		{
			return SubAgentName.IsValidSubAgentName(name);
		}

		protected override string NormalizeName(string name)
		{
			return SubAgentName.ToValidSubAgentName(name);
		}
	}
}
