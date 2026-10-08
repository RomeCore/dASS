using LLMDesktopAssistant.SlashCommands;
using LLMDesktopAssistant.SlashCommands.Resolution;

namespace LLMDesktopAssistant.Tests.SlashCommands
{
	/// <summary>
	/// The token grammar and the total order of the command matcher.
	/// </summary>
	public class SlashCommandMatcherTests
	{
		private static SlashCommandInfo Command(string name, string[] namespaces, int order = 0, int overrideOrder = 0,
			string[]? aliases = null) => new()
		{
			Name = name,
			Namespaces = [.. namespaces],
			Order = order,
			OverrideOrder = overrideOrder,
			Aliases = aliases is null ? [] : [.. aliases]
		};

		[Theory]
		[InlineData("hello world", false, "", "")]
		[InlineData("/name", true, "name", "")]
		[InlineData("/name args", true, "name", "args")]
		[InlineData("  /name spaced", true, "name", "spaced")]
		[InlineData("/name    padded", true, "name", "padded")]
		[InlineData("/", true, "", "")]
		[InlineData("//escaped", false, "", "")]
		[InlineData("   //escaped", false, "", "")]
		[InlineData("", false, "", "")]
		public void TryExtractToken_Cases(string text, bool expected, string token, string arguments)
		{
			var result = SlashCommandMatcher.TryExtractToken(text, out var actualToken, out var actualArguments);

			Assert.Equal(expected, result);
			Assert.Equal(token, actualToken);
			Assert.Equal(arguments, actualArguments);
		}

		[Fact]
		public void TryExtractToken_KeepsAMultiLineRemainder()
		{
			Assert.True(SlashCommandMatcher.TryExtractToken("/name first\nsecond third", out var token, out var args));

			Assert.Equal("name", token);
			Assert.Equal("first\nsecond third", args);
		}

		[Theory]
		[InlineData("//foo", "/foo")]
		[InlineData("//", "/")]
		[InlineData("  //foo", "  /foo")]
		[InlineData("/foo", "/foo")]
		[InlineData("plain", "plain")]
		public void UnescapeLeadingSlash_Cases(string text, string expected)
			=> Assert.Equal(expected, SlashCommandMatcher.UnescapeLeadingSlash(text));

		[Theory]
		[InlineData("grilling", true, "grilling")]
		[InlineData("skill:grilling", true, "grilling")]
		[InlineData("skill:matt-pocock:grilling", true, "grilling")]
		[InlineData("", false, "")]
		[InlineData("skill:", false, "")]
		[InlineData(":grilling", false, "grilling")]
		[InlineData("a::b", false, "b")]
		public void ParseToken_NameAndValidity(string token, bool valid, string name)
		{
			var parsed = SlashCommandMatcher.ParseToken(token);

			Assert.Equal(valid, parsed.IsValid);
			Assert.Equal(name, parsed.Name);
			Assert.Equal(token, parsed.Raw);
		}

		[Fact]
		public void ParseToken_SplitsQualifiers()
		{
			var parsed = SlashCommandMatcher.ParseToken("skill:matt-pocock:grilling");

			Assert.Equal(new[] { "skill", "matt-pocock" }, parsed.Qualifiers);
		}

		[Fact]
		public void Match_ByBareName()
		{
			var command = Command("grilling", ["skill"]);

			var result = SlashCommandMatcher.Match([command], SlashCommandMatcher.ParseToken("grilling"));

			Assert.Same(command, Assert.Single(result).Command);
		}

		[Fact]
		public void Match_ByTypeByPackByBoth_AndOrderIndependent()
		{
			var command = Command("grilling", ["skill", "matt-pocock"]);
			var commands = new[] { command };

			Assert.Single(SlashCommandMatcher.Match(commands, SlashCommandMatcher.ParseToken("skill:grilling")));
			Assert.Single(SlashCommandMatcher.Match(commands, SlashCommandMatcher.ParseToken("matt-pocock:grilling")));
			Assert.Single(SlashCommandMatcher.Match(commands, SlashCommandMatcher.ParseToken("skill:matt-pocock:grilling")));
			// Set semantics: the qualifier order does not matter.
			Assert.Single(SlashCommandMatcher.Match(commands, SlashCommandMatcher.ParseToken("matt-pocock:skill:grilling")));
		}

		[Fact]
		public void Match_ByAlias()
		{
			var command = Command("grilling", ["skill"], aliases: ["grill"]);

			var result = SlashCommandMatcher.Match([command], SlashCommandMatcher.ParseToken("grill"));

			Assert.Same(command, Assert.Single(result).Command);
		}

		[Fact]
		public void Match_IsCaseInsensitive()
		{
			var command = Command("Grilling", ["Skill"]);

			Assert.Single(SlashCommandMatcher.Match([command], SlashCommandMatcher.ParseToken("SKILL:grilling")));
		}

		[Fact]
		public void Match_NoMatch_IsEmpty()
		{
			var command = Command("grilling", ["skill"]);

			Assert.Empty(SlashCommandMatcher.Match([command], SlashCommandMatcher.ParseToken("nope")));
			Assert.Empty(SlashCommandMatcher.Match([command], SlashCommandMatcher.ParseToken("other-pack:grilling")));
		}

		[Fact]
		public void Match_AnInvalidToken_IsEmpty()
		{
			var command = Command("grilling", ["skill"]);

			Assert.Empty(SlashCommandMatcher.Match([command], SlashCommandMatcher.ParseToken("")));
			Assert.Empty(SlashCommandMatcher.Match([command], SlashCommandMatcher.ParseToken("skill:")));
		}

		[Fact]
		public void Match_SameNameInDifferentNamespaces_BothMatchABareToken()
		{
			var skill = Command("grilling", ["skill"]);
			var agent = Command("grilling", ["agent"]);

			var result = SlashCommandMatcher.Match([skill, agent], SlashCommandMatcher.ParseToken("grilling"));

			Assert.Equal(2, result.Count);
			Assert.All(result, candidate => Assert.Equal("grilling", candidate.Command.Name));
		}

		[Fact]
		public void Match_OrdersByTierThenOrder()
		{
			var derived = Command("grilling", ["agent"], order: 0, overrideOrder: SlashCommandOrderTiers.Derived);
			var native = Command("grilling", ["native"], order: 0, overrideOrder: SlashCommandOrderTiers.Native);

			var result = SlashCommandMatcher.Match([derived, native], SlashCommandMatcher.ParseToken("grilling"));

			Assert.Same(native, result[0].Command);
			Assert.False(result[0].IsDefeated);
			Assert.Same(derived, result[1].Command);
			Assert.True(result[1].IsDefeated);
		}

		[Fact]
		public void Match_TiesAreBrokenByTheFullyQualifiedKey()
		{
			var b = Command("grilling", ["b"], order: 1, overrideOrder: 0);
			var a = Command("grilling", ["a"], order: 1, overrideOrder: 0);

			var result = SlashCommandMatcher.Match([b, a], SlashCommandMatcher.ParseToken("grilling"));

			Assert.Equal(new[] { "a:grilling", "b:grilling" }, result.Select(c => c.Command.Key));
		}
	}
}
