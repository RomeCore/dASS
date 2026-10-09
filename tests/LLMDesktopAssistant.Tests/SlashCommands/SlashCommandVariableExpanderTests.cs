using System.Collections.Immutable;
using LLMDesktopAssistant.Prompting.Skills;
using LLMDesktopAssistant.SlashCommands.Arguments;
using LLMDesktopAssistant.SlashCommands.Providers;

namespace LLMDesktopAssistant.Tests.SlashCommands
{
	/// <summary>
	/// The skill-body variable expansion: the built-ins (<c>$ARGUMENTS</c>, the skill directory and name), the
	/// process environment, and leaving an unknown variable verbatim.
	/// </summary>
	public class SlashCommandVariableExpanderTests
	{
		private static SlashCommandBoundArguments Arguments(string rest) => new()
		{
			Positionals = [],
			Keyed = ImmutableDictionary<string, ParsedSlashCommandArgument>.Empty,
			RawPositionalArguments = rest,
			RestPositionalArguments = rest
		};

		private static SkillInfo Skill(string name = "grilling", string? home = null, string? path = null) => new()
		{
			Name = name,
			Description = "d",
			HomeDirectory = home,
			Path = path
		};

		[Fact]
		public void Arguments_AreSubstituted()
			=> Assert.Equal("Do: do it", SlashCommandVariableExpander.ExpandSkill("Do: $ARGUMENTS", Arguments("do it"), Skill()));

		[Fact]
		public void BracedArguments_AreSubstituted()
			=> Assert.Equal("Do: do it",
				SlashCommandVariableExpander.ExpandSkill("Do: ${ARGUMENTS}", Arguments("do it"), Skill()));

		[Fact]
		public void AnUnknownVariable_IsLeftVerbatim()
			=> Assert.Equal("$NOPE and ${ALSO_NOPE}",
				SlashCommandVariableExpander.ExpandSkill("$NOPE and ${ALSO_NOPE}", Arguments("x"), Skill()));

		[Fact]
		public void TheSkillDirectory_ResolvesFromTheHomeDirectory()
		{
			var skill = Skill(home: @"C:\skills\grilling");

			Assert.Equal(@"C:\skills\grilling", SlashCommandVariableExpander.ExpandSkill("$CLAUDE_SKILL_DIR", Arguments(""), skill));
			Assert.Equal(@"C:\skills\grilling", SlashCommandVariableExpander.ExpandSkill("$SKILL_DIR", Arguments(""), skill));
		}

		[Fact]
		public void TheSkillDirectory_FallsBackToTheFilePath()
			=> Assert.Equal(@"C:\skills\grilling",
				SlashCommandVariableExpander.ExpandSkill("$CLAUDE_SKILL_DIR", Arguments(""), Skill(path: @"C:\skills\grilling\SKILL.md")));

		[Fact]
		public void TheSkillName_Resolves()
			=> Assert.Equal("grilling", SlashCommandVariableExpander.ExpandSkill("$SKILL_NAME", Arguments(""), Skill("grilling")));

		[Fact]
		public void AProcessEnvironmentVariable_Resolves()
		{
			const string name = "DASS_SLASH_COMMAND_TEST_VAR";
			Environment.SetEnvironmentVariable(name, "from-env");
			try
			{
				Assert.Equal("from-env", SlashCommandVariableExpander.ExpandSkill("$" + name, Arguments(""), Skill()));
			}
			finally
			{
				Environment.SetEnvironmentVariable(name, null);
			}
		}

		[Fact]
		public void APositionalMarker_IsLeftVerbatim()
			=> Assert.Equal("$1 $2", SlashCommandVariableExpander.ExpandSkill("$1 $2", Arguments("x"), Skill()));

		[Fact]
		public void ABodyWithoutVariables_IsUnchanged()
			=> Assert.Equal("no variables here", SlashCommandVariableExpander.ExpandSkill("no variables here", Arguments("x"), Skill()));

		[Fact]
		public void GetSkillVariable_AnUnknownName_ReturnsNull()
			=> Assert.Null(SlashCommandVariableExpander.GetSkillVariable("NOPE", Arguments("x"), Skill()));
	}
}
