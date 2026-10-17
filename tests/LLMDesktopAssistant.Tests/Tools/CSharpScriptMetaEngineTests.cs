using System.Text.Json.Nodes;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Scripting;
using LLMDesktopAssistant.Scripting.CSX;
using LLMDesktopAssistant.Tests.Tools;
using LLMDesktopAssistant.Tools;
using LLMDesktopAssistant.Tools.Scripting;

namespace LLMDesktopAssistant.Tests.Tools;

public class CSharpScriptMetaEngineTests
{
	private static readonly IScriptableToolEngineDescriptor Descriptor = new CSharpScriptToolEngineDescriptor();
	private static readonly CSharpScriptToolEngine Engine = new(new CSharpScriptService());
	private static readonly ScriptableToolParser Parser = new([Engine]);

	private static ToolInfo CreateTool(string executionCode) => new()
	{
		Name = "get_weather",
		NameKey = Locale.GetKey("Weather Checker"),
		Description = "Gets the current weather for a location.",
		CategoryKey = Locale.GetKey("weather"),
		ApprovalLevel = ToolApprovalLevel.PolicyBased,
		DefaultExpectedBehaviour = ToolBehaviour.None,
		ArgumentSchema = new JsonObject
		{
			["type"] = "object",
			["properties"] = new JsonObject
			{
				["location"] = new JsonObject { ["type"] = "string" }
			}
		},
		ScriptLanguage = ScriptLanguageType.CSharpScript,
		Body = executionCode
	};

	[Fact]
	public async Task Execute_Script_StreamsResultAndReturnsStructuredValue()
	{
		var tool = CreateTool("""
			var city = (string?)ToolArgs?["city"];
			Result.Write("City: " + city);
			Result.SetStatus("Map", "Looking up...");
			return new { ok = true, city };
			""");
		var executor = Engine.CreateExecutor(tool);
		var args = JsonNode.Parse("""{ "city": "New York" }""");
		var session = ToolTestContext.Create(tool, args);

		var result = await executor.ExecuteAsync(args, session.Context, CancellationToken.None);

		Assert.True(result.Success);
		Assert.Contains("City: New York", session.Call.ResultContent);
		// Icon kinds are canonicalized to "<pack>:<data>" (a bare "Map" means the material pack).
		Assert.Equal("material:Map", session.Call.StatusIcon?.ToString());
		Assert.Equal("Looking up...", session.Call.StatusTitle);
		Assert.Equal(true, session.Call.StructuredResult?["ok"]?.GetValue<bool>());
		Assert.Equal("New York", session.Call.StructuredResult?["city"]?.GetValue<string>());
	}

	[Fact]
	public async Task Execute_Script_MissingOptionalArguments_DoesNotFail()
	{
		var tool = CreateTool("""
			var days = (int?)ToolArgs?["days"] ?? 10;
			Result.Write("Days: " + days);
			return days;
			""");
		var executor = Engine.CreateExecutor(tool);
		var session = ToolTestContext.Create(tool);

		var result = await executor.ExecuteAsync(null, session.Context, CancellationToken.None);

		Assert.True(result.Success);
		Assert.Contains("Days: 10", session.Call.ResultContent);
		Assert.Equal(10, session.Call.StructuredResult?.GetValue<int>());
	}

	[Fact]
	public async Task Execute_ScriptWithCompilationError_Fails()
	{
		var tool = CreateTool("var x = ;");
		var executor = Engine.CreateExecutor(tool);
		var session = ToolTestContext.Create(tool);

		var result = await executor.ExecuteAsync(null, session.Context, CancellationToken.None);

		Assert.False(result.Success);
		Assert.Contains("Compilation errors", session.Call.ResultContent);
	}

	[Fact]
	public async Task Execute_ScriptThrowingException_Fails()
	{
		var tool = CreateTool("throw new InvalidOperationException(\"boom\");");
		var executor = Engine.CreateExecutor(tool);
		var session = ToolTestContext.Create(tool);

		var result = await executor.ExecuteAsync(null, session.Context, CancellationToken.None);

		Assert.False(result.Success);
		Assert.Contains("boom", session.Call.ResultContent);
	}
}
