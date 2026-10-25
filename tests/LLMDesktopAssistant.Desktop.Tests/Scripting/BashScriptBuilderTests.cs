using System.Collections.Immutable;
using LLMDesktopAssistant.Desktop.Scripting.ExternalProcess;
using LLMDesktopAssistant.Scripting;
using LLMDesktopAssistant.StructuredValues;
using LLMDesktopAssistant.StructuredValues.Const;

namespace LLMDesktopAssistant.Desktop.Tests.Scripting;

public class BashScriptBuilderTests
{
	private static ExternalProcessScriptTerminalParameters Parameters(string body,
		params (string Name, INodeValue? Value)[] variables)
	{
		var values = ImmutableDictionary.CreateBuilder<string, INodeValue?>();
		foreach (var (name, value) in variables)
			values[name] = value;

		return new ExternalProcessScriptTerminalParameters
		{
			Kind = ScriptableAddonKind.Tool,
			Body = body,
			Variables = values.ToImmutable()
		};
	}

	[Theory]
	[InlineData("plain", "'plain'")]
	[InlineData("with space", "'with space'")]
	[InlineData("it's", "'it'\\''s'")]
	[InlineData("", "''")]
	[InlineData("кириллица", "'кириллица'")]
	[InlineData("line\nbreak", "'line\nbreak'")]
	[InlineData("\"double\"", "'\"double\"'")]
	public void SingleQuoted_KeepsEverythingInsideOneLiteral(string value, string expected)
		=> Assert.Equal(expected, BashScriptBuilder.SingleQuoted(value));

	[Fact]
	public void Build_WritesAnAssignmentPerVariableThenTheBody()
	{
		var script = BashScriptBuilder.Build(Parameters("echo \"$tool_args\"",
			("tool_args", new ConstNodeStringValue { Value = "it's" })));

		Assert.Equal("tool_args='\"it'\\''s\"'\necho \"$tool_args\"", script);
	}

	[Fact]
	public void Build_PassesTheValueAsJson()
	{
		var script = BashScriptBuilder.Build(Parameters("body", ("value", new ConstNodeStringValue { Value = "text" })));

		Assert.StartsWith("value='\"text\"'\n", script);
	}

	[Fact]
	public void Build_KeepsCyrillicReadable()
	{
		var script = BashScriptBuilder.Build(Parameters("body", ("value", new ConstNodeStringValue { Value = "кириллица" })));

		Assert.StartsWith("value='\"кириллица\"'\n", script);
	}

	[Fact]
	public void Build_NormalizesCrlfInTheBody()
	{
		var script = BashScriptBuilder.Build(Parameters("echo one\r\necho two\r\n"));

		Assert.Equal("echo one\necho two\n", script);
	}

	[Fact]
	public void Build_NormalizesLoneCarriageReturns()
	{
		Assert.Equal("a\nb", BashScriptBuilder.NormalizeLineEndings("a\rb"));
	}
}
