using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Addons.Parsers;
using LLMDesktopAssistant.Addons.Parsers.Frontmatter;
using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.SlashCommands.Arguments;
using LLMDesktopAssistant.Tools;

namespace LLMDesktopAssistant.Prompting.Skills
{
	/// <summary>
	/// The parser for <c>SKILL.md</c> / <c>SKILL.mdx</c> addon files.
	/// </summary>
	[Service(typeof(IAddonFileParser<SkillInfo>))]
	public class SkillParser : FrontmatterBasedAddonParser<SkillInfo, SkillChange>
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

		protected override void Populate(SkillInfo addon, AddonFrontmatterDocument frontmatter, ref AddonDiagnostic? diagnostic)
		{
			base.Populate(addon, frontmatter, ref diagnostic);

			addon.AllowedTools = frontmatter.Get<ImmutableList<ToolNameWithSpecifier>>("allowed-tools", []);
			addon.AvailableTools = frontmatter.Get<ImmutableList<ToolNameWithSpecifier>>("available-tools", []);
			addon.DisallowedTools = frontmatter.Get<ImmutableList<ToolNameWithSpecifier>>("disallowed-tools", []);

			var injectionMode = frontmatter.Get<string>("injection-mode");
			if (!string.IsNullOrWhiteSpace(injectionMode))
			{
				switch (injectionMode.Trim().ToLowerInvariant())
				{
					case "default":
						addon.InjectionMode = SkillInjectionMode.Default;
						break;

					case "full":
						addon.InjectionMode = SkillInjectionMode.Full;
						break;

					default:
						diagnostic = diagnostic.Combine(new AddonDiagnostic
						{
							Messages = [$"Unknown injection mode '{injectionMode}'. Expected 'default' or 'full'."]
						});
						break;
				}
			}

			if (frontmatter.TryRequest("disable-model-invocation", ref diagnostic, out bool disableModelInvocation))
				addon.DisableModelInvocation = disableModelInvocation;

			if (frontmatter.TryRequest("user-invocable", ref diagnostic, out bool userInvocable))
				addon.UserInvocable = userInvocable;

			var argumentHint = frontmatter.Get<string>("argument-hint");
			addon.ArgumentHint = string.IsNullOrWhiteSpace(argumentHint) ? null : argumentHint.Trim();

			addon.ArgumentSchema = SkillArgumentHintParser.Build(addon.ArgumentHint, out bool invalidArgumentHint);
			if (invalidArgumentHint)
			{
				diagnostic = diagnostic.Combine(new AddonDiagnostic
				{
					Codes = AddonDiagnosticCode.InvalidArgumentHint,
					Messages = [$"Failed to parse the 'argument-hint': '{addon.ArgumentHint}'."]
				});
			}

			addon.ChatAvailablePredicate = (skill, services) => !addon.DisableModelInvocation;
		}

		protected override bool IsValidName(string name)
		{
			return SkillName.IsValidSkillName(name);
		}

		protected override string NormalizeName(string name)
		{
			return SkillName.ToValidSkillName(name);
		}

		protected override bool IsNameMatchingFile(string name, AddonPathInfo fileInfo)
		{
			return name.Equals(GetFallbackName(fileInfo), StringComparison.Ordinal);
		}
	}
}
