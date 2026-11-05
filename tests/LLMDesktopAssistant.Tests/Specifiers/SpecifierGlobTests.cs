using LLMDesktopAssistant.Tools.Specifiers;
using LLMDesktopAssistant.Utils.Patterns;

namespace LLMDesktopAssistant.Tests.Specifiers;

/// <summary>
/// A specifier literal is a flat glob pattern, or a filesystem pattern when the tool argument it
/// targets is a path. These tests pin both dialects and the target-kind dispatch that turns one rule
/// text into two different compilations.
/// </summary>
public class SpecifierGlobTests
{
	private static readonly string Root = Path.Combine(Path.GetTempPath(), "dass-specifier-root");

	private static SpecifierMatchResult Match(string pattern, string text) =>
		SpecifierMatcher.Match(SpecifierParser.Parse(pattern), [text], []);

	[Theory]
	[InlineData("git {commit,merge} *", "git commit -m x", SpecifierMatchResult.FullMatch)]
	[InlineData("git {commit,merge} *", "git push", SpecifierMatchResult.NoMatch)]
	[InlineData("fs-read path=*.{cs,md}", "fs-read path=a.md", SpecifierMatchResult.FullMatch)]
	[InlineData("ls [ab].txt", "ls a.txt", SpecifierMatchResult.FullMatch)]
	[InlineData("ls [ab].txt", "ls c.txt", SpecifierMatchResult.NoMatch)]
	[InlineData("ls [*].txt", "ls *.txt", SpecifierMatchResult.FullMatch)]
	[InlineData("git status:*", "git status", SpecifierMatchResult.FullMatch)]
	[InlineData("git status:*", "git status\t--short", SpecifierMatchResult.FullMatch)]
	[InlineData("git status:*", "git statusx", SpecifierMatchResult.NoMatch)]
	public void FlatDialect_GlobConstructs(string pattern, string text, SpecifierMatchResult expected)
	{
		Assert.Equal(expected, Match(pattern, text));
	}

	[Fact]
	public void FlatDialect_StarCrossesLineBreaks()
	{
		// A quoted argument may carry a line break; '*' has to cover it, or a deny rule is bypassable.
		Assert.Equal(SpecifierMatchResult.FullMatch, Match("rm *", "rm -rf \"a\nb\""));
	}

	[Fact]
	public void FlatDialect_DoubleStarIsAnError_NotASilentMiss()
	{
		Assert.Throws<GlobException>(() => Match("src/**/*.cs", "src/a.cs"));
	}

	[Fact]
	public void PathTarget_MatchesRelativeAndAbsolutePaths()
	{
		var specifier = SpecifierParser.Parse("**/*.cs");

		Assert.Equal(SpecifierMatchResult.FullMatch, SpecifierMatcher.Match(specifier,
			[SpecifierTarget.Path("src/Lib/File.cs", Root)]));
		Assert.Equal(SpecifierMatchResult.FullMatch, SpecifierMatcher.Match(specifier,
			[SpecifierTarget.Path(Path.Combine(Root, "src", "Lib", "File.cs"), Root)]));
		Assert.Equal(SpecifierMatchResult.NoMatch, SpecifierMatcher.Match(specifier,
			[SpecifierTarget.Path("src/Lib/File.md", Root)]));
	}

	[Fact]
	public void PathTarget_PrefixBecomesTheRoot()
	{
		var specifier = SpecifierParser.Parse("src/**/*.cs");

		Assert.Equal(SpecifierMatchResult.FullMatch, SpecifierMatcher.Match(specifier,
			[SpecifierTarget.Path("src/Lib/File.cs", Root)]));
		Assert.Equal(SpecifierMatchResult.NoMatch, SpecifierMatcher.Match(specifier,
			[SpecifierTarget.Path("docs/Lib/File.cs", Root)]));
		Assert.Equal(SpecifierMatchResult.NoMatch, SpecifierMatcher.Match(specifier,
			[SpecifierTarget.Path(Path.Combine(Root, "src", "..", "docs", "File.cs"), Root)]));
	}

	[Fact]
	public void PathTarget_WithoutBaseDirectory_Throws()
	{
		var specifier = SpecifierParser.Parse("*.cs");

		Assert.Throws<ArgumentException>(() => SpecifierMatcher.Match(specifier,
			[new SpecifierTarget("a.cs", SpecifierTargetKind.Path)]));
	}

	[Fact]
	public void TextTarget_WithAPathPattern_Throws()
	{
		// A path rule aimed at a prose argument is a mistake, not an empty file system.
		var specifier = SpecifierParser.Parse("src/**/*.cs");

		Assert.Throws<GlobException>(() => SpecifierMatcher.Match(specifier, [SpecifierTarget.Text("src/a.cs")]));
	}
}
