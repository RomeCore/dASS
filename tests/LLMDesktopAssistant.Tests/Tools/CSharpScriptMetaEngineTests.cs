using System.Text.Json.Nodes;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Scripting;
using LLMDesktopAssistant.Scripting.CSX;
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

	private static ToolExecutionContext CreateDummyContext()
	{
		var toolInfo = new ToolInfo
		{
			Name = "get_weather",
			Description = "Gets the current weather for a location.",
			ArgumentSchema = new JsonObject { ["type"] = "object" },
			Executor = (_, _, _) => Task.FromResult(new ReactiveToolResult())
		};
		return ToolExecutionContext.CreateDummy(toolInfo, null, null);
	}

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

		var reactiveResult = await executor(args, CreateDummyContext(), CancellationToken.None);
		var success = await reactiveResult.Completion;

		Assert.True(success);
		Assert.Contains(reactiveResult.ResultContentLines, line => line == "City: New York");
		// Icon kinds are canonicalized to "<pack>:<data>" (a bare "Map" means the material pack).
		Assert.Equal("material:Map", reactiveResult.StatusIcon?.ToString());
		Assert.Equal("Looking up...", reactiveResult.StatusTitle);
		Assert.Equal(true, reactiveResult.StructuredResult?["ok"]?.GetValue<bool>());
		Assert.Equal("New York", reactiveResult.StructuredResult?["city"]?.GetValue<string>());
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

		var reactiveResult = await executor(null, CreateDummyContext(), CancellationToken.None);
		var success = await reactiveResult.Completion;

		Assert.True(success);
		Assert.Contains(reactiveResult.ResultContentLines, line => line == "Days: 10");
		Assert.Equal(10, reactiveResult.StructuredResult?.GetValue<int>());
	}

	[Fact]
	public async Task Execute_ScriptWithCompilationError_Fails()
	{
		var tool = CreateTool("var x = ;");
		var executor = Engine.CreateExecutor(tool);

		var reactiveResult = await executor(null, CreateDummyContext(), CancellationToken.None);
		var success = await reactiveResult.Completion;

		Assert.False(success);
		Assert.Contains(reactiveResult.ResultContentLines, line => line.Contains("Compilation errors"));
	}

	[Fact]
	public async Task Execute_ScriptThrowingException_Fails()
	{
		var tool = CreateTool("throw new InvalidOperationException(\"boom\");");
		var executor = Engine.CreateExecutor(tool);

		var reactiveResult = await executor(null, CreateDummyContext(), CancellationToken.None);
		var success = await reactiveResult.Completion;

		Assert.False(success);
		Assert.Contains(reactiveResult.ResultContentLines, line => line.Contains("boom"));
	}
}
