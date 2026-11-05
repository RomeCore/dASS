using LLMDesktopAssistant.Tools;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Tools.Execution.Executors;
using LLMDesktopAssistant.Tools.Specifiers;
using LLMDesktopAssistant.Utils.Patterns;

namespace LLMDesktopAssistant.Tests.Specifiers;

/// <summary>
/// The validator is the card's half of the safety net: it compiles every literal in the dialect of the
/// argument it targets, so a rule that would be inert at runtime is refused while it is being written.
/// </summary>
public class SpecifierPatternValidatorTests
{
	private static readonly ToolArgumentsDescriptor PathArguments =
		ToolMethodBinder.DescribeArguments((Func<string, string, string>)Copy);

	private static readonly ToolArgumentsDescriptor TextArguments =
		ToolMethodBinder.DescribeArguments((Func<string, bool, string>)Run);

	private static readonly ToolArgumentsDescriptor ArrayArguments =
		ToolMethodBinder.DescribeArguments((Func<string[], string>)Arrays);

	private static string Copy(
		[MainArgument] [PathArgument(File = true, Directory = false)] string path,
		[PathArgument(File = true, Directory = false)] string destination) => path;

	private static string Run([MainArgument] string command, bool runTerminal = false) => command;

	private static string Arrays(string[] items) => string.Join(',', items);

	[Theory]
	[InlineData("**/*.cs")]
	[InlineData("src/**/*.cs && destination:src/*.tmp")]
	[InlineData("destination:*.tmp")]
	public void ValidPathRules_HaveNoDiagnostics(string pattern)
	{
		Assert.Empty(SpecifierPatternValidator.Validate(pattern, PathArguments));
	}

	[Fact]
	public void EmptyRule_IsReported()
	{
		var diagnostic = Assert.Single(SpecifierPatternValidator.Validate("   ", PathArguments));

		Assert.Equal(SpecifierDiagnosticKind.Empty, diagnostic.Kind);
	}

	[Fact]
	public void GrammarFailure_IsReportedAtItsPosition()
	{
		var diagnostic = Assert.Single(SpecifierPatternValidator.Validate("git * ||", TextArguments));

		Assert.Equal(SpecifierDiagnosticKind.Syntax, diagnostic.Kind);
	}

	[Fact]
	public void DoubleStarInATextTarget_IsReported()
	{
		var diagnostic = Assert.Single(SpecifierPatternValidator.Validate("git **", TextArguments));

		Assert.Equal(SpecifierDiagnosticKind.Pattern, diagnostic.Kind);
		Assert.Equal(GlobError.DoubleStarUnsupported, diagnostic.Error);
	}

	[Fact]
	public void ReversedClassRange_IsReported()
	{
		var diagnostic = Assert.Single(SpecifierPatternValidator.Validate("git [z-a]", TextArguments));

		Assert.Equal(SpecifierDiagnosticKind.Pattern, diagnostic.Kind);
		Assert.Equal(GlobError.InvalidClassRange, diagnostic.Error);

		// The whole literal is underlined: its start is the anchor, its length is the span to paint.
		Assert.Equal(0, diagnostic.Position);
		Assert.Equal("git [z-a]".Length, diagnostic.Length);
	}

	[Fact]
	public void NonTerminalArgumentTail_IsReported()
	{
		var diagnostic = Assert.Single(SpecifierPatternValidator.Validate("git status:* now", TextArguments));

		Assert.Equal(SpecifierDiagnosticKind.Pattern, diagnostic.Kind);
		Assert.Equal(GlobError.ArgumentTailNotTerminal, diagnostic.Error);
	}

	[Fact]
	public void UnnamedLiteralWithoutAMainArgument_IsReported()
	{
		var diagnostic = Assert.Single(SpecifierPatternValidator.Validate("*.cs", ArrayArguments));

		Assert.Equal(SpecifierDiagnosticKind.NoTarget, diagnostic.Kind);
	}

	[Fact]
	public void ValidTextRule_HasNoDiagnostics()
	{
		Assert.Empty(SpecifierPatternValidator.Validate("git status:* && runTerminal:true", TextArguments));
	}
}
