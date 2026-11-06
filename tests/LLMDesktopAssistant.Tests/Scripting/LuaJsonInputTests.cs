using System.Text.Json.Nodes;
using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Agents;
using LLMDesktopAssistant.Scripting;
using LLMDesktopAssistant.Scripting.Lua;
using LLMDesktopAssistant.Scripting.Lua.Addons;
using LLMDesktopAssistant.Tests.Tools;
using Microsoft.Extensions.DependencyInjection;

namespace LLMDesktopAssistant.Tests.Scripting;

public class LuaJsonInputTests
{
	private sealed class StubScriptSetCollector : IAddonSetCollector<LuaScriptInfo>
	{
		public AddonSet<LuaScriptInfo> GetAvailableAddons()
		{
			return [];
		}

		public AddonSet<LuaScriptInfo> GetAddonsForChat()
		{
			return [];
		}

		public AddonSet<LuaScriptInfo> GetAddonsForAgent(ChatAgentDescriptor agent)
		{
			return [];
		}
	}

	private static LuaToolEngine Engine() => new();

	private static async Task<string> RunLuaToolAsync(ScriptInputMode mode, string body, JsonObject args)
	{
		var tool = ToolTestContext.CreateTool("lua-args", info =>
		{
			info.Body = body;
			info.InputMode = mode;
		});

		var executor = Engine().CreateExecutor(tool);
		var session = ToolTestContext.Create(tool, args, configureServices: services =>
		{
			services.AddSingleton<IAddonSetCollector<LuaScriptInfo>, StubScriptSetCollector>();
			services.AddSingleton<LuaService>();
		});

		await executor.ExecuteAsync(args, session.Context, CancellationToken.None);
		return session.Call.ResultContent ?? string.Empty;
	}

	[Theory]
	[InlineData(ScriptInputMode.Json, "string")]
	[InlineData(ScriptInputMode.Auto, "table")]
	[InlineData(ScriptInputMode.Structured, "table")]
	public async Task ToolArgs_ArrivesAsTheModeSays(ScriptInputMode mode, string expectedLuaType)
	{
		var output = await RunLuaToolAsync(mode, "print(type(tool_args))", new JsonObject { ["text"] = "hi" });

		Assert.Contains(expectedLuaType, output);
	}

	[Fact]
	public async Task ToolArgs_AsJsonText_IsTheJsonOfTheArguments()
	{
		var output = await RunLuaToolAsync(ScriptInputMode.Json, "print(tool_args)", new JsonObject { ["text"] = "hi" });

		Assert.Contains("""{"text":"hi"}""", output);
	}
}
