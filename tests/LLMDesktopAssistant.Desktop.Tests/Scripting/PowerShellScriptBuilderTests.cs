using System.Collections.Immutable;
using LLMDesktopAssistant.Desktop.Scripting.ExternalProcess;
using LLMDesktopAssistant.Scripting;
using LLMDesktopAssistant.StructuredValues;
using LLMDesktopAssistant.StructuredValues.Const;

namespace LLMDesktopAssistant.Desktop.Tests.Scripting;

public class PowerShellScriptBuilderTests
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
	[InlineData("it's", "'it''s'")]
	[InlineData("''", "''''''")]
	[InlineData("", "''")]
	[InlineData("кириллица", "'кириллица'")]
	[InlineData("line\nbreak", "'line\nbreak'")]
	[InlineData("\"double\"", "'\"double\"'")]
	public void SingleQuoted_DoublesTheApostropheAndKeepsEverythingElse(string value, string expected)
		=> Assert.Equal(expected, PowerShellScriptBuilder.SingleQuoted(value));

	[Fact]
	public void Build_GuardsTheConsoleEncodingByVersionNotByPlatform()
	{
		var script = PowerShellScriptBuilder.Build(Parameters("Write-Output 'hi'"), ScriptInputMode.Json);

		Assert.StartsWith(
			"if ($PSVersionTable.PSVersion.Major -le 5) { [Console]::OutputEncoding = [Text.Encoding]::UTF8 }\r\n", script);
	}

	[Fact]
	public void Build_AssignsAJsonStringWhenTheScriptParsesIt()
	{
		var script = PowerShellScriptBuilder.Build(
			Parameters("body", ("tool_args", new ConstNodeStringValue { Value = "it's" })), ScriptInputMode.Json);

		Assert.Contains("$tool_args = '\"it''s\"'\r\n", script);
	}

	[Fact]
	public void Build_PipesThroughConvertFromJsonForAStructuredValue()
	{
		var script = PowerShellScriptBuilder.Build(
			Parameters("body", ("tool_args", new ConstNodeStringValue { Value = "text" })), ScriptInputMode.Structured);

		Assert.Contains("$tool_args = '\"text\"' | ConvertFrom-Json\r\n", script);
	}

	[Fact]
	public void Build_KeepsCyrillicReadable()
	{
		var script = PowerShellScriptBuilder.Build(
			Parameters("body", ("value", new ConstNodeStringValue { Value = "кириллица" })), ScriptInputMode.Json);

		Assert.Contains("кириллица", script);
	}

	[Fact]
	public void Build_NormalizesTheBodyToCrlf()
	{
		var script = PowerShellScriptBuilder.Build(Parameters("Write-Output one\nWrite-Output two\n"), ScriptInputMode.Json);

		Assert.EndsWith("Write-Output one\r\nWrite-Output two\r\n", script);
		Assert.DoesNotContain("\n", script.Replace("\r\n", string.Empty));
	}
}
