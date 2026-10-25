using System.Collections.Immutable;
using LLMDesktopAssistant.Desktop.Scripting.ExternalProcess;
using LLMDesktopAssistant.Scripting;
using LLMDesktopAssistant.StructuredValues;
using LLMDesktopAssistant.StructuredValues.Const;

namespace LLMDesktopAssistant.Desktop.Tests.Scripting;

public class BatchScriptBuilderTests
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

	[Fact]
	public void Build_StartsBySilencingTheEchoAndTheDelayedExpansion()
	{
		var script = BatchScriptBuilder.Build(Parameters("echo hi"));

		Assert.StartsWith("@echo off\r\nsetlocal DisableDelayedExpansion\r\n", script);
	}

	[Fact]
	public void Build_WritesOneSetPerVariableBeforeTheBody()
	{
		var script = BatchScriptBuilder.Build(Parameters("echo %tool_args%", ("tool_args", new ConstNodeStringValue { Value = "text" })));

		Assert.Equal("""
			@echo off
			setlocal DisableDelayedExpansion
			set "tool_args="text""
			echo %tool_args%
			""".ReplaceLineEndings("\r\n"), script);
	}

	[Fact]
	public void Build_DoublesPercentSoTheValueStaysLiteral()
	{
		var script = BatchScriptBuilder.Build(Parameters("body", ("tool_args", new ConstNodeStringValue { Value = "%PATH%" })));

		Assert.Contains("set \"tool_args=\"%%PATH%%\"\"\r\n", script);
	}

	[Fact]
	public void Build_EscapesNonAsciiInsteadOfTrustingTheCodePage()
	{
		var script = BatchScriptBuilder.Build(Parameters("body", ("tool_args", new ConstNodeStringValue { Value = "кириллица" })));

		Assert.DoesNotContain("кириллица", script);
		Assert.Contains(@"\u043A", script);
	}

	[Fact]
	public void Build_KeepsTheInnerQuotesOfTheJson()
	{
		var script = BatchScriptBuilder.Build(Parameters("body", ("a", new ConstNodeStringValue { Value = "x" })));

		Assert.Contains("set \"a=\"x\"\"\r\n", script);
	}

	[Fact]
	public void Build_NormalizesTheBodyToCrlf()
	{
		var script = BatchScriptBuilder.Build(Parameters("echo one\necho two\n"));

		Assert.EndsWith("echo one\r\necho two\r\n", script);
		Assert.DoesNotContain("\n", script.Replace("\r\n", string.Empty));
	}

	[Fact]
	public void Build_RefusesAPreambleLineCmdCannotHold()
	{
		var oversized = new ConstNodeStringValue { Value = new string('a', BatchScriptBuilder.MaxLineLength) };

		var exception = Assert.Throws<InvalidOperationException>(
			() => BatchScriptBuilder.Build(Parameters("body", ("tool_args", oversized))));

		Assert.Contains("tool_args", exception.Message);
		Assert.Contains(BatchScriptBuilder.MaxLineLength.ToString(), exception.Message);
	}

	[Fact]
	public void Build_AcceptsAPreambleLineAtTheLimit()
	{
		// The `set "name=value"` wrapper and the doubled JSON quotes are part of the line, so the payload is shorter.
		var payload = new string('a', BatchScriptBuilder.MaxLineLength - 20);

		var script = BatchScriptBuilder.Build(Parameters("body", ("tool_args", new ConstNodeStringValue { Value = payload })));

		Assert.Contains($"set \"tool_args=\"{payload}\"\"\r\n", script);
	}
}
