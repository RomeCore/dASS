using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.Tools;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Tools.Execution.Executors;
using LLMDesktopAssistant.Tools.Execution.Support;
using LLMDesktopAssistant.Tools.Scripting;

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

		public IToolMainExecutor CreateExecutor(ToolInfo tool)
		{
			return new DelegateMainExecutor(async (args, ctx, cancellationToken) =>
			{
				var result = new LuaToolResult(ctx.Call);

				// The execution context flows through AsyncLocal, so it does not have to live in the globals.
				LuaExecutionContext.SetTool(ctx, result);

				try
				{
					var luaService = ctx.Services.GetRequiredService<LuaService>();
					var scriptResult = await luaService.ExecuteAsync(tool.Body, print => result.Write(print), g =>
					{
						g["tool_args"] = StructuredLuaConverter.JsonNodeToLuaValue(args);
					}, cancellationToken);
					if (ctx.Call.StructuredResult is null)
						ctx.Call.SetStructuredResult(StructuredLuaConverter.LuaValueToJsonNode(scriptResult));
					result.Write("Script returned: " + scriptResult.ToString());
					result.Complete(success: true);
				}
				catch (LuaRuntimeException srex)
				{
					result.Write("Caught error: " + srex.Message);
					result.Complete(success: false);
				}
				catch (Exception ex)
				{
					result.Write("Caught error: " + ex.Message);
					result.Complete(success: false);
				}

				return result.ToExecutionResult();
			});
		}
	}
}
