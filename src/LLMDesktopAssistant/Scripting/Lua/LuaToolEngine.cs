using System.Text.Json.Nodes;
using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.Tools;
using LLMDesktopAssistant.Tools.Scripting;
using Microsoft.Extensions.DependencyInjection;

namespace LLMDesktopAssistant.Scripting.Lua
{
	/// <summary>
	/// Lua implementation of <see cref="IScriptableToolEngine"/>.
	/// Handles tools written in Lua with YAML frontmatter in `--[[ ... ]]` blocks.
	/// </summary>
	[Service(typeof(IScriptableToolEngine))]
	public class LuaToolEngine : IScriptableToolEngine
	{
		public ScriptLanguageType Language => ScriptLanguageType.Lua;

		public IScriptableToolEngineDescriptor Descriptor { get; } = new LuaToolEngineDescriptor();

		public Func<JsonNode?, ToolExecutionContext, CancellationToken, Task<ReactiveToolResult>> CreateExecutor(ToolInfo tool)
		{
			return (JsonNode? args, ToolExecutionContext context, CancellationToken cancellationToken) =>
			{
				var reactiveResult = new ReactiveToolResult();

				_ = Task.Run(async () =>
				{
					// The execution context flows through AsyncLocal, so it does not have to live in the globals.
					LuaExecutionContext.Set(tool: context, reactiveResult: reactiveResult);

					try
					{
						var luaService = context.Chat.Services.GetRequiredService<LuaService>();
						var scriptResult = await luaService.ExecuteAsync(tool.Body, print => reactiveResult.ResultContentLines.Add(print), g =>
						{
							g["tool_args"] = StructuredLuaConverter.JsonNodeToLuaValue(args);
						});
						reactiveResult.StructuredResult ??= StructuredLuaConverter.LuaValueToJsonNode(scriptResult);
						reactiveResult.ResultContentLines.Add($"Script returned: " + scriptResult.ToString());
						reactiveResult.TryCompleteWithSuccess();
					}
					catch (LuaRuntimeException srex)
					{
						reactiveResult.ResultContentLines.Add("Caught error: " + srex.Message);
						reactiveResult.TryCompleteWithError();
					}
					catch (Exception ex)
					{
						reactiveResult.ResultContentLines.Add("Caught error: " + ex.Message);
						reactiveResult.TryCompleteWithError();
					}
				}, CancellationToken.None);

				return Task.FromResult(reactiveResult);
			};
		}
	}
}
