using LLMDesktopAssistant.Tools.Specifiers;

namespace LLMDesktopAssistant.Tests.Specifiers;

/// <summary>
/// The tokenizer feeds the card's syntax highlighting. It is looser than the grammar on purpose: a
/// highlighter must never fail on a half-written rule.
/// </summary>
public class SpecifierPatternTokenizerTests
{
	private static readonly string[] Names = ["runTerminal", "path"];

	[Fact]
	public void Separators_AreTokenized()
	{
		var tokens = SpecifierPatternTokenizer.Tokenize("a && b || c", Names);

		Assert.Equal(
			[new SpecifierToken(SpecifierTokenKind.Separator, 2, 2), new SpecifierToken(SpecifierTokenKind.Separator, 7, 2)],
			tokens);
	}

	[Fact]
	public void DeclaredParameter_IsTokenizedWithItsColon()
	{
		var tokens = SpecifierPatternTokenizer.Tokenize("runTerminal:true", Names);
		var token = Assert.Single(tokens);

		Assert.Equal(SpecifierTokenKind.Parameter, token.Kind);
		Assert.Equal(0, token.Start);
		Assert.Equal("runTerminal:".Length, token.Length);
	}

	[Fact]
	public void UndeclaredName_StaysPlainText()
	{
		Assert.Empty(SpecifierPatternTokenizer.Tokenize("command:true", Names));
	}

	[Fact]
	public void WildcardsClassesAndBraces_AreTokenized()
	{
		var tokens = SpecifierPatternTokenizer.Tokenize("[a-c]*.{cs,md}", Names);

		Assert.Equal(
			[
				new SpecifierToken(SpecifierTokenKind.Class, 0, 5),
				new SpecifierToken(SpecifierTokenKind.Wildcard, 5, 1),
				new SpecifierToken(SpecifierTokenKind.Brace, 7, 1),
				new SpecifierToken(SpecifierTokenKind.Brace, 13, 1),
			],
			tokens);
	}

	[Fact]
	public void DoubleStar_IsOneWildcard()
	{
		var token = Assert.Single(SpecifierPatternTokenizer.Tokenize("**", Names));

		Assert.Equal(SpecifierTokenKind.Wildcard, token.Kind);
		Assert.Equal(2, token.Length);
	}

	[Fact]
	public void UnterminatedClass_StaysPlainText()
	{
		Assert.Empty(SpecifierPatternTokenizer.Tokenize("[a", Names));
	}

	[Fact]
	public void EscapedSeparator_IsNotASeparator()
	{
		Assert.Empty(SpecifierPatternTokenizer.Tokenize(@"a \|| b", Names));
	}
}
