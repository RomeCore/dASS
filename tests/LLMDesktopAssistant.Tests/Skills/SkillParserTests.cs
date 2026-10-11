using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Prompting.Skills;
using LLMDesktopAssistant.SlashCommands.Arguments;
using LLMDesktopAssistant.StructuredValues;

namespace LLMDesktopAssistant.Tests.Skills;

public class SkillParserTests
{
	private static readonly SkillParser Parser = new();

	private static SkillInfo Parse(string content, string? path = null)
		=> Parser.Parse(content, new AddonPathInfo(path ?? "C:\\skills\\test-skill\\SKILL.md", false)).First();

	[Fact]
	public void MinimalFrontmatter_ParsesNameAndDescription()
	{
		var skill = Parse("""
			---
			name: test-skill
			description: A test skill for unit testing.
			---
			""");

		Assert.Equal("test-skill", skill.Name);
		Assert.Equal("A test skill for unit testing.", skill.Description);
		Assert.Empty(skill.BodyGetter(skill));
	}

	[Fact]
	public void FullFrontmatter_ParsesAllFields()
	{
		var skill = Parse("""
			---
			name: pdf-processing
			description: Extract text from PDFs, fill forms, merge documents.
			license: MIT
			compatibility: Requires Python 3.14+
			allowed-tools: Bash(python:*) Read Write
			tags:
			  - pdf
			  - document
			  - extraction
			metadata:
			  author: example-org
			  version: "2.1"
			---
			""");

		Assert.Equal("pdf-processing", skill.Name);
		Assert.Equal("Extract text from PDFs, fill forms, merge documents.", skill.Description);
		Assert.Equal("MIT", skill.Metadata[AddonMetadataType.License]);
		Assert.Equal("Requires Python 3.14+", skill.Metadata[AddonMetadataType.Compatibility]);
		Assert.Equal("example-org", skill.Metadata[AddonMetadataType.Author]);
		Assert.Equal("2.1", skill.Metadata[AddonMetadataType.Version]);
		Assert.Equal([new("Bash", "python:*"), new("Read"), new("Write")], skill.AllowedTools);
		Assert.Equal(["pdf", "document", "extraction"], skill.Tags);
	}

	[Fact]
	public void AllowedToolsAsList_ParsesCorrectly()
	{
		var skill = Parse("""
			---
			name: multi-tool
			description: Skill with multiple tools.
			allowed-tools:
			  - Bash(git:*)
			  - Read
			  - Write
			  - Bash(docker:*)
			---
			""");

		Assert.Equal([new("Bash", "git:*"), new("Read"), new("Write"), new("Bash", "docker:*")], skill.AllowedTools);
	}

	[Fact]
	public void AllowedToolsAsString_ParsesCorrectly()
	{
		var skill = Parse("""
			---
			name: string-tools
			description: Skill with space-separated tools.
			allowed-tools: Bash(git:*) Read Write
			disallowed-tools: Bash(docker:*) Python
			---
			""");

		Assert.Equal([new("Bash", "git:*"), new("Read"), new("Write")], skill.AllowedTools);
		Assert.Equal([new("Bash", "docker:*"), new("Python")], skill.DisallowedTools);
	}

	[Fact]
	public void BodyAfterFrontmatter_IsPreserved()
	{
		var skill = Parse("""
			---
			name: body-test
			description: Skill with body content.
			---
			# Instructions

			Step 1: Do something.
			Step 2: Do another thing.

			## Notes
			Some notes here.
			""");

		Assert.Contains("Step 1: Do something.", skill.BodyGetter(skill));
		Assert.Contains("Step 2: Do another thing.", skill.BodyGetter(skill));
		Assert.Contains("## Notes", skill.BodyGetter(skill));
	}

	[Fact]
	public void BodyWithoutFrontmatter_UsesHeaderAsFallbackName()
	{
		var skill = Parse("""
			# My Custom Skill

			This skill does something useful.
			Use this skill when you need to do X.

			Detailed instructions go here.
			""");

		Assert.Equal("my-custom-skill", skill.Name); // Parser slugifies skill name
		Assert.Equal("""
			This skill does something useful.
			Use this skill when you need to do X.
			""", skill.Description);
		Assert.Contains("Detailed instructions go here.", skill.BodyGetter(skill));
	}

	[Fact]
	public void BodyWithoutFrontmatterAndWithoutHeader_UsesFolderName()
	{
		var skill = Parse("Just some plain text content.", "C:\\skills\\fallback-name\\SKILL.md");

		Assert.Equal("fallback-name", skill.Name);
		Assert.Equal("Just some plain text content.", skill.Description);
		Assert.Equal("Just some plain text content.", skill.Body);
	}

	[Fact]
	public void HeaderWithoutFrontmatter_FirstParagraphIsDescription()
	{
		var skill = Parse("""
			# Code Review

			Reviews pull requests for code quality.
			Make sure to check style and tests.

			Some other content.
			""");

		Assert.Equal("code-review", skill.Name);
		Assert.Equal("""
			Reviews pull requests for code quality.
			Make sure to check style and tests.
			""", skill.Description);
	}

	[Fact]
	public void AdditionalMetadata_PreservesUnknownKeys()
	{
		var skill = Parse("""
			---
			name: custom-meta
			description: Skill with custom metadata.
			metadata:
			  author: me
			  version: "1.0"
			  x-custom-field: custom-value
			  department: engineering
			---
			""");

		Assert.Equal("me", skill.Metadata[AddonMetadataType.Author]);
		Assert.Equal("1.0", skill.Metadata[AddonMetadataType.Version]);
		Assert.Equal("custom-value", skill.AdditionalMetadata["x-custom-field"]);
		Assert.Equal("engineering", skill.AdditionalMetadata["department"]);
	}

	[Fact]
	public void AdditionalProperties_StoresNonStandardRootFields()
	{
		var skill = Parse("""
			---
			name: extra-fields
			description: Skill with extra root fields.
			priority: high
			x-my-field: hello
			---
			""");

		Assert.Equal("high", skill.AdditionalProperties["priority"]!.AsString());
		Assert.Equal("hello", skill.AdditionalProperties["x-my-field"]!.AsString());
	}

	[Fact]
	public void EmptyFrontmatter_UsesFallbacks()
	{
		var skill = Parse("""
			---
			---
			# My Skill

			Description from paragraph.
			""");

		Assert.Equal("my-skill", skill.Name);
		Assert.Equal("Description from paragraph.", skill.Description);
	}

	[Fact]
	public void FrontmatterWithOnlyName_UsesHeaderAndParagraphFallback()
	{
		var skill = Parse("""
			---
			name: only-name
			---
			# Header Title

			First paragraph as description.
			""");

		Assert.Equal("only-name", skill.Name);
		Assert.Equal("First paragraph as description.", skill.Description);
	}

	[Fact]
	public void FrontmatterWithNameAndDescription_IgnoresHeaderFallback()
	{
		var skill = Parse("""
			---
			name: explicit-name
			description: Explicit description.
			---
			# Header Title

			First paragraph.
			""");

		Assert.Equal("explicit-name", skill.Name);
		Assert.Equal("Explicit description.", skill.Description);
	}

	[Fact]
	public void PathProperty_ContainsFullPath()
	{
		var path = "C:\\my-project\\.agents\\skills\\my-skill\\SKILL.md";
		var skill = Parse("""
			---
			name: my-skill
			description: Skill with path.
			---
			""", path);

		Assert.Equal(path, skill.Path);
	}

	[Fact]
	public void HomeDirectory_IsParentOfSkillFile()
	{
		var skill = Parse("""
			---
			name: home-dir-test
			description: Testing home directory.
			---
			""", "C:\\skills\\my-tool\\SKILL.md");

		Assert.Equal("C:\\skills\\my-tool", skill.HomeDirectory);
	}

	[Fact]
	public void Tags_NotSpecified_ReturnsEmpty()
	{
		var skill = Parse("""
			---
			name: no-tags
			description: Skill without tags.
			---
			""");

		Assert.Empty(skill.Tags);
	}

	[Fact]
	public void AllowedTools_NotSpecified_ReturnsEmpty()
	{
		var skill = Parse("""
			---
			name: no-tools
			description: Skill without allowed tools.
			---
			""");

		Assert.Empty(skill.AllowedTools);
	}

	[Fact]
	public void Metadata_NotSpecified_ReturnsEmpty()
	{
		var skill = Parse("""
			---
			name: no-meta
			description: Skill without metadata.
			---
			""");

		Assert.Empty(skill.Metadata);
		Assert.Empty(skill.AdditionalMetadata);
		Assert.Empty(skill.AdditionalProperties);
	}

	[Fact]
	public void PreservesWhitespaceInBody()
	{
		var skill = Parse("""
			---
			name: whitespace
			description: Whitespace preservation.
			---
			# Instructions
			
			  Indented line.
			  
			Another line.
			""");

		Assert.Contains("  Indented line.", skill.BodyGetter(skill));
		Assert.Contains("Another line.", skill.BodyGetter(skill));
	}

	[Fact(Skip = "YamlDotNet issue with unescaped colons in scalar values")]
	public void DescriptionWithColons_ParsesCorrectly()
	{
		// Colon in unquoted YAML value can break naive parsers
		var skill = Parse("""
			---
			name: colon-desc
			description: Use this skill when: the user asks about PDFs
			---
			""");

		Assert.Equal("colon-desc", skill.Name);
		Assert.Equal("Use this skill when: the user asks about PDFs", skill.Description);
	}

	[Fact]
	public void MultipleLineDescription_ParsesCorrectly()
	{
		var skill = Parse("""
			---
			name: multi-line-desc
			description: >
			  This is a multi-line
			  description that spans
			  several lines.
			---
			""");

		Assert.Equal("multi-line-desc", skill.Name);
		Assert.Contains("multi-line", skill.Description);
		Assert.Contains("spans", skill.Description);
	}

	[Fact]
	public void LicenseField_ParsesCorrectly()
	{
		var skill = Parse("""
			---
			name: licensed-skill
			description: A skill with a license.
			license: Apache-2.0
			---
			""");

		Assert.Equal("Apache-2.0", skill.Metadata[AddonMetadataType.License]);
	}

	[Fact]
	public void AdditionalProperties_PreservesYamlNodes()
	{
		var skill = Parse("""
			---
			name: complex-prop
			description: Skill with complex additional property.
			x-config:
			  key1: value1
			  key2: value2
			---
			""");

		Assert.True(skill.AdditionalProperties.ContainsKey("x-config"));
	}

	[Fact]
	public void ArgumentHint_BuildsTheArgumentSchema()
	{
		var skill = Parse("""
			---
			name: with-hint
			description: Skill with an argument hint.
			argument-hint: "[issue-number] [files...:$a|b:%The files]"
			---
			""");

		Assert.Equal("[issue-number] [files...:$a|b:%The files]", skill.ArgumentHint);

		var schema = Assert.IsType<SlashCommandArgumentSchema>(skill.ArgumentSchema);
		Assert.Equal("issue-number", Assert.Single(schema.Positionals).Name.Key);
		Assert.Equal("files", schema.RestPositional!.Name.Key);
		Assert.IsType<ChoiceArgumentFormatProvider>(schema.RestPositional.Format);
	}

	[Fact]
	public void NoArgumentHint_YieldsTheDefaultRestSlot()
	{
		var skill = Parse("""
			---
			name: no-hint
			description: Skill without an argument hint.
			---
			""");

		Assert.Null(skill.ArgumentHint);

		var rest = Assert.IsType<SlashCommandArgument>(skill.ArgumentSchema!.RestPositional);
		Assert.Equal("command.argument.skill.arguments", rest.Name.Key);
	}

	[Fact]
	public void AnInvalidArgumentHint_ReportsTheDiagnostic()
	{
		var skill = Parse("""
			---
			name: bad-hint
			description: Skill with a broken argument hint.
			argument-hint: "[a] [b"
			---
			""");

		Assert.NotNull(skill.Diagnostic);
		Assert.True(skill.Diagnostic!.Codes.HasFlag(AddonDiagnosticCode.InvalidArgumentHint));

		Assert.Equal("[a] [b", skill.ArgumentSchema!.RestPositional!.Description!.Key);
	}
}
