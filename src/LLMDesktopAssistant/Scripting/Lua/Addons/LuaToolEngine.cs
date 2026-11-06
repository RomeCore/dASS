using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.StructuredValues.Converters;
using LLMDesktopAssistant.Tools;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Tools.Execution.Executors;
using LLMDesktopAssistant.Tools.Execution.Support;
using LLMDesktopAssistant.Tools.Scripting;

namespace LLMDesktopAssistant.Scripting.Lua.Addons
{
	/// <summary>
	/// Lua implementation of <see cref="IScriptableToolEngine"/>.
	/// Handles tools written in Lua with YAML frontmatter in `--[[ ... ]]` blocks.
	/// </summary>
	[Service(typeof(IScriptableToolEngine))]
	public class LuaToolEngine : IScriptableToolEngine
	{
		public IReadOnlyList<IScriptEngineDescriptor> Descriptors { get; } = [LuaScriptEngineDescriptor.Instance];

		public IToolMainExecutor CreateExecutor(ToolInfo tool)
		{
			return new DelegateMainExecutor(async (args, ctx, cancellationToken) =>
			{
				var result = LuaExecutionContext.ToolResult;
				if (result is null)
					LuaExecutionContext.SetTool(ctx, result = new LuaToolResult(ctx.Call));

				try
				{
					var luaService = ctx.Services.GetRequiredService<LuaService>();
					var scriptResult = await luaService.ExecuteAsync(tool.Body, print => result.Write(print), g =>
					{
						// json-input: true hands the arguments over as JSON text the body decodes itself; auto and
						// false keep the native table, which is what a Lua script usually wants.
						g["tool_args"] = tool.InputMode is ScriptInputMode.Json
							? new LuaString(args?.ToJsonString() ?? "null")
							: args.ToConstNodeValue().ToLuaValue();
					}, cancellationToken);
					if (ctx.Call.StructuredResult is null)
						ctx.Call.SetStructuredResult(scriptResult.ToConstNodeValue(tolerant: true).ToJsonNode());
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
