using System.Reflection;
using System.Text.Json.Nodes;
using LLMDesktopAssistant.Agents.Tasks;
using LLMDesktopAssistant.Tools;
using RCLargeLanguageModels.Json.Schema;

namespace LLMDesktopAssistant.Tests.Agents;

/// <summary>
/// Tests for the <see cref="DelegateAgentTool"/> class.
/// </summary>
public class DelegateAgentToolTests
{
	private static class SyncHandlers
	{
		/// <summary>
		/// A synchronous handler that echoes the value.
		/// </summary>
		public static AgentToolCallResult Echo(string value) => Ok($"echo:{value}");

		/// <summary>
		/// A synchronous handler with a required and an optional parameter.
		/// </summary>
		public static AgentToolCallResult Sum(int a, int b = 10) => Ok($"sum:{a + b}");

		/// <summary>
		/// A synchronous handler with an optional parameter only.
		/// </summary>
		public static AgentToolCallResult Optional(string value = "default") => Ok($"opt:{value}");

		/// <summary>
		/// A synchronous handler with a renamed parameter.
		/// </summary>
		public static AgentToolCallResult Renamed([Name("renamed_param")] string value) => Ok($"renamed:{value}");

		/// <summary>
		/// A synchronous handler that reports the cancellation state.
		/// </summary>
		public static AgentToolCallResult CheckCancellation(CancellationToken ct) =>
			ct.IsCancellationRequested ? Ok("cancelled") : Ok("not-cancelled");

		/// <summary>
		/// A synchronous handler that throws.
		/// </summary>
		public static AgentToolCallResult Throws() => throw new InvalidOperationException("boom");

		/// <summary>
		/// A handler with a wrong return type.
		/// </summary>
		public static string WrongReturnType() => "nope";

		/// <summary>
		/// A handler with multiple cancellation token parameters.
		/// </summary>
		public static AgentToolCallResult TwoTokens(CancellationToken a, CancellationToken b) => Ok("tokens");
	}

	private sealed class InstanceHandler
	{
		/// <summary>
		/// An instance handler that greets the name.
		/// </summary>
		public AgentToolCallResult Greet(string name) => Ok($"hi:{name}");
	}

	private static AgentToolCallResult Ok(string content) => new() { Content = content, Success = true };

	private static DelegateAgentTool CreateTool<TDelegate>(string name = "tool", string? displayName = null, string description = "desc", TDelegate? executor = null)
		where TDelegate : Delegate
		=> new(name, displayName, description, ToolSource.Unknown, executor!);

	[Fact]
	public void Ctor_WithDelegate_SetsMetadata()
	{
		var tool = CreateTool("echo", "Echo Tool", "Echoes the value", (Func<string, AgentToolCallResult>)SyncHandlers.Echo);

		Assert.Equal("echo", tool.Name);
		Assert.Equal("Echo Tool", tool.DisplayName);
		Assert.Equal("Echoes the value", tool.Description);
	}

	[Fact]
	public void Ctor_DisplayNameNull_FallsBackToName()
	{
		var tool = CreateTool("echo", null, "desc", (Func<string, AgentToolCallResult>)SyncHandlers.Echo);

		Assert.Equal("echo", tool.DisplayName);
	}

	[Fact]
	public void Ctor_NullMethod_ThrowsArgumentNullException()
	{
		Assert.Throws<ArgumentNullException>(() => new DelegateAgentTool("t", null, "d", ToolSource.Unknown, null, null!));
	}

	[Fact]
	public void Ctor_InstanceMethodWithNullTarget_ThrowsArgumentException()
	{
		var method = typeof(InstanceHandler).GetMethod(nameof(InstanceHandler.Greet))!;

		Assert.Throws<ArgumentException>(() => new DelegateAgentTool("t", null, "d", ToolSource.Unknown, null, method));
	}

	[Fact]
	public void Ctor_StaticMethodWithNullTarget_Succeeds()
	{
		var method = typeof(SyncHandlers).GetMethod(nameof(SyncHandlers.Echo), BindingFlags.Public | BindingFlags.Static)!;

		var tool = new DelegateAgentTool("echo", null, "d", ToolSource.Unknown, null, method);

		Assert.NotNull(tool.ArgumentSchema);
	}

	[Fact]
	public void Ctor_WrongReturnType_ThrowsArgumentException()
	{
		var method = typeof(SyncHandlers).GetMethod(nameof(SyncHandlers.WrongReturnType), BindingFlags.Public | BindingFlags.Static)!;

		Assert.Throws<ArgumentException>(() => new DelegateAgentTool("t", null, "d", ToolSource.Unknown, null, method));
	}

	[Fact]
	public void Ctor_MultipleCancellationTokenParameters_ThrowsArgumentException()
	{
		var method = typeof(SyncHandlers).GetMethod(nameof(SyncHandlers.TwoTokens), BindingFlags.Public | BindingFlags.Static)!;

		Assert.Throws<ArgumentException>(() => new DelegateAgentTool("t", null, "d", ToolSource.Unknown, null, method));
	}

	[Fact]
	public void ArgumentSchema_ContainsRequiredAndOptionalParameters()
	{
		var tool = CreateTool("sum", executor: (Func<int, int, AgentToolCallResult>)SyncHandlers.Sum);

		var schema = tool.ArgumentSchema;
		Assert.Equal("object", (string?)schema["type"]);
		Assert.False((bool?)schema["additionalProperties"]);

		var properties = schema["properties"]!.AsObject();
		Assert.True(properties.ContainsKey("a"));
		Assert.True(properties.ContainsKey("b"));

		var required = schema["required"]!.AsArray().Select(n => (string?)n).ToList();
		Assert.Contains("a", required);
		Assert.DoesNotContain("b", required);
	}

	[Fact]
	public void ArgumentSchema_ExcludesCancellationToken()
	{
		var tool = CreateTool("ct", executor: (Func<CancellationToken, AgentToolCallResult>)SyncHandlers.CheckCancellation);

		var properties = tool.ArgumentSchema["properties"]!.AsObject();
		Assert.False(properties.ContainsKey("ct"));
	}

	[Fact]
	public void ArgumentSchema_UsesNameAttribute()
	{
		var tool = CreateTool("renamed", executor: (Func<string, AgentToolCallResult>)SyncHandlers.Renamed);

		var properties = tool.ArgumentSchema["properties"]!.AsObject();
		Assert.True(properties.ContainsKey("renamed_param"));
		Assert.False(properties.ContainsKey("value"));
	}

	[Fact]
	public async Task PreExecute_ReturnsNoneBehaviour()
	{
		var tool = CreateTool("echo", executor: (Func<string, AgentToolCallResult>)SyncHandlers.Echo);

		var preResult = await tool.PreExecuteAsync(null);

		Assert.Equal(ToolBehaviour.None, preResult.ExpectedBehaviour);
	}

	[Fact]
	public async Task Execute_SyncMethod_ReturnsResult()
	{
		var tool = CreateTool("echo", executor: (Func<string, AgentToolCallResult>)SyncHandlers.Echo);

		var result = await tool.ExecuteAsync(JsonNode.Parse("""{"value":"hello"}"""), null);

		Assert.True(result.Success);
		Assert.Equal("echo:hello", result.Content);
	}

	[Fact]
	public async Task Execute_AsyncMethod_AwaitsTask()
	{
		var tool = CreateTool("async", executor: (Func<string, Task<AgentToolCallResult>>)AsyncHandlers.Async);

		var result = await tool.ExecuteAsync(JsonNode.Parse("""{"value":"hello"}"""), null);

		Assert.True(result.Success);
		Assert.Equal("async:hello", result.Content);
	}

	[Fact]
	public async Task Execute_WithArguments_DeserializesByParameterName()
	{
		var tool = CreateTool("sum", executor: (Func<int, int, AgentToolCallResult>)SyncHandlers.Sum);

		var result = await tool.ExecuteAsync(JsonNode.Parse("""{"a":5,"b":7}"""), null);

		Assert.True(result.Success);
		Assert.Equal("sum:12", result.Content);
	}

	[Fact]
	public async Task Execute_OptionalParameter_UsesDefault()
	{
		var tool = CreateTool("sum", executor: (Func<int, int, AgentToolCallResult>)SyncHandlers.Sum);

		var result = await tool.ExecuteAsync(JsonNode.Parse("""{"a":5}"""), null);

		Assert.True(result.Success);
		Assert.Equal("sum:15", result.Content);
	}

	[Fact]
	public async Task Execute_NullArguments_UsesDefaults()
	{
		var tool = CreateTool("opt", executor: (Func<string, AgentToolCallResult>)SyncHandlers.Optional);

		var result = await tool.ExecuteAsync(null, null);

		Assert.True(result.Success);
		Assert.Equal("opt:default", result.Content);
	}

	[Fact]
	public async Task Execute_NonObjectArguments_TreatedAsEmpty()
	{
		var tool = CreateTool("opt", executor: (Func<string, AgentToolCallResult>)SyncHandlers.Optional);

		var result = await tool.ExecuteAsync(JsonValue.Create(42), null);

		Assert.True(result.Success);
		Assert.Equal("opt:default", result.Content);
	}

	[Fact]
	public async Task Execute_NameAttribute_MapsArguments()
	{
		var tool = CreateTool("renamed", executor: (Func<string, AgentToolCallResult>)SyncHandlers.Renamed);

		var result = await tool.ExecuteAsync(JsonNode.Parse("""{"renamed_param":"x"}"""), null);

		Assert.True(result.Success);
		Assert.Equal("renamed:x", result.Content);
	}

	[Fact]
	public async Task Execute_NumberAsString_Converts()
	{
		var tool = CreateTool("sum", executor: (Func<int, int, AgentToolCallResult>)SyncHandlers.Sum);

		var result = await tool.ExecuteAsync(JsonNode.Parse("""{"a":"5","b":"7"}"""), null);

		Assert.True(result.Success);
		Assert.Equal("sum:12", result.Content);
	}

	[Fact]
	public async Task Execute_CancellationToken_IsPassedToMethod()
	{
		var tool = CreateTool("ct", executor: (Func<CancellationToken, AgentToolCallResult>)SyncHandlers.CheckCancellation);
		using var cts = new CancellationTokenSource();
		cts.Cancel();

		var result = await tool.ExecuteAsync(null, null, cts.Token);

		Assert.True(result.Success);
		Assert.Equal("cancelled", result.Content);
	}

	[Fact]
	public async Task Execute_DeserializationError_ReturnsFailure()
	{
		var tool = CreateTool("sum", executor: (Func<int, int, AgentToolCallResult>)SyncHandlers.Sum);

		var result = await tool.ExecuteAsync(JsonNode.Parse("""{"a":"abc"}"""), null);

		Assert.False(result.Success);
		Assert.StartsWith("Failed to deserialize arguments", result.Content);
	}

	[Fact]
	public async Task Execute_MethodThrows_ReturnsFailure()
	{
		var tool = CreateTool("throws", executor: (Func<AgentToolCallResult>)SyncHandlers.Throws);

		var result = await tool.ExecuteAsync(null, null);

		Assert.False(result.Success);
		Assert.StartsWith("Error executing tool", result.Content);
	}

	[Fact]
	public async Task Execute_MissingRequiredValueTypeParameter_ReturnsFailure()
	{
		var tool = CreateTool("sum", executor: (Func<int, int, AgentToolCallResult>)SyncHandlers.Sum);

		var result = await tool.ExecuteAsync(JsonNode.Parse("{}"), null);

		Assert.False(result.Success);
		Assert.StartsWith("Failed to deserialize arguments", result.Content);
	}

	[Fact]
	public async Task Execute_InstanceMethodWithTarget_Works()
	{
		var handler = new InstanceHandler();
		var method = typeof(InstanceHandler).GetMethod(nameof(InstanceHandler.Greet))!;
		var tool = new DelegateAgentTool("greet", null, "Greets", ToolSource.Unknown, handler, method);

		var result = await tool.ExecuteAsync(JsonNode.Parse("""{"name":"Bob"}"""), null);

		Assert.True(result.Success);
		Assert.Equal("hi:Bob", result.Content);
	}

	private static class AsyncHandlers
	{
		/// <summary>
		/// An asynchronous handler that echoes the value.
		/// </summary>
		public static async Task<AgentToolCallResult> Async(string value)
		{
			await Task.Yield();
			return Ok($"async:{value}");
		}
	}
}
