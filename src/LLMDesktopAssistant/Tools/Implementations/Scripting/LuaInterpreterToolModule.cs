using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Scripting.Lua;
using LLMDesktopAssistant.Utils;
using Material.Icons;

namespace LLMDesktopAssistant.Tools.Implementations.Scripting
{
	[ToolModule]
	public class LuaInterpreterToolModule : ToolModule
	{
		private readonly LuaService _lua;

		public LuaInterpreterToolModule(LuaService lua)
		{
			_lua = lua;

			AddTool(new ToolInitializationInfo
			{
				Executor = Execute,
				StreamingAnalyzer = ExecuteStreaming,
				PreviewExecutor = ExecutePreview,
				Name = "lua-execute",
				Description = $"""
					# MAIN INFO
					Lua is executing using AsyncLua 5.5+{typeof(LuaState).Assembly.GetName().Version?.ToString() ?? ""}.
					Executes Lua and returns the script result along with messages printed by 'print' function
					(the `dass.tool.result.write` works in a similar way).
					Lua has the API to interact with the application (called dASS).

					# AsyncLua changes
					You can use `async/await` in your scripts, for example:
					local async function doWork()
						await task.delay(100)
						return 'done'
					end
					print(await doWork())
					For full AsyncLua manuals see `print(manuals(asynclua))`

					# SMART UX WITH STREAMING AND STATUS ICONS/TITLES
					You can also use the `dass.tool.result` for streaming output, progress and status
					(for meta-tools and long-running scripts):
			
					-- 1. Basic streaming output with status icon (from Material Icons)
					dass.tool.result.set_status("Download", "Processing...") -- "Download" is the icon name, "Processing..." is the title
					dass.tool.result.write("Step 1: Starting...")
					time.sleep(100)
					dass.tool.result.write("Step 2: Working...")
					time.sleep(100)
					dass.tool.result.write("Step 3: Done!")
					dass.tool.result.complete_with_success()

					-- 2. Progress bar and Markdown output
					dass.tool.result.use_markdown(true)
					dass.tool.result.set_status("ChartTimeline", "Processing...")
					dass.tool.result.set_progress(0, 0, 10) -- current, min, max
					for i = 1, 10 do
					  dass.tool.result.set_progress(i)
					  dass.tool.result.write(string.format("  - **Item %d** completed", i))
					  time.sleep(100) -- simulate work
					end
					dass.tool.result.set_progress(1.0)
					dass.tool.result.set_status("Check", "All done!")
					dass.tool.result.complete_with_success()

					-- 3. Structured result + error handling
					local ok, data = pcall(fs.read, "data.json")
					if not ok then
					  dass.tool.result.set_status("AlertCircle", "File not found")
					  dass.tool.result.write("Error: " .. data)
					  dass.tool.result.complete_with_error()
					  return
					end
					local parsed = json.decode(data)
					dass.tool.result.set_structured(parsed)
					dass.tool.result.set_status("FileCheck", "Loaded")
					dass.tool.result.complete_with_success()
					
					# SEE MANUALS BEFORE USING THE API
					Use `manuals(...)` function to get the documentation for a specific namespace:
					`print(manuals(_G, dass.agents, dass.tool, dass.tool.result))` for example.
					To get list of all available namespaces, use `print(namespaces())`
					""",
				NameKey = Locale.GetKey("tool.name.lua-execute"),
				DescriptionKey = Locale.GetKey("tool.description.lua-execute"),
				CategoryKey = Locale.GetConstKey("Lua"),
				DefaultExpectedBehaviour = ToolBehaviour.PossiblyUnexpected
			});
		}

		private StreamingToolArgumentsAnalysisResult ExecuteStreaming(string? lua)
		{
			int lines = 0;
			if (lua != null)
				foreach (var line in lua.EnumerateLines())
					lines++;

			return new StreamingToolArgumentsAnalysisResult
			{
				StatusIcon = MaterialIconKind.LanguageLua,
				StatusTitle = LocalizationManager.LocalizeStaticFormat("tool.status.script.lines", lines)
			};
		}

		private PreviewToolExecutionResult ExecutePreview(string lua)
		{
			return new PreviewToolExecutionResult
			{
				StatusIcon = MaterialIconKind.LanguageLua,
				StatusTitle = null
			};
		}

		private ReactiveToolResult Execute(
			string lua,
			ToolExecutionContext context,
			bool isolatedExecution = true,
			CancellationToken cancellationToken = default)
		{
			var reactiveResult = new ReactiveToolResult
			{
				StatusIcon = MaterialIconKind.LanguageLua,
				StatusTitle = null
			};

			_ = Task.Run(async () =>
			{
				try
				{
					LuaTuple scriptResult;
					if (isolatedExecution)
					{
						scriptResult = await _lua.ExecuteAsync(lua, print => reactiveResult.ResultContentLines.Add(print), g =>
						{
							g[LuaVariables.ToolExecutionContext] = LuaValueConverter.ToLuaValue(context);
							g[LuaVariables.ToolReactiveResult] = LuaValueConverter.ToLuaValue(reactiveResult);
						}, cancellationToken);
					}
					else
					{
						scriptResult = await _lua.ExecuteAsync(lua, print => reactiveResult.ResultContentLines.Add(print), g =>
						{
							g[LuaVariables.ToolExecutionContext] = LuaValueConverter.ToLuaValue(context);
							g[LuaVariables.ToolReactiveResult] = LuaValueConverter.ToLuaValue(reactiveResult);
						}, cancellationToken);
					}

					reactiveResult.TryCompleteWithSuccess();
				}
				catch (LuaRuntimeException srex)
				{
					reactiveResult.ResultContentLines.Add("Caught error: " + (DebugHelper.IsDebug ? srex.ToString() : srex.Message));
					reactiveResult.ResultContentLines.Add("Remember to read the manuals for API");
					reactiveResult.TryCompleteWithError();
				}
				catch (Exception ex)
				{
					reactiveResult.ResultContentLines.Add("Caught error: " + (DebugHelper.IsDebug ? ex.ToString() : ex.Message));
					reactiveResult.ResultContentLines.Add("Remember to read the manuals for API");
					reactiveResult.TryCompleteWithError();
				}
				finally
				{
				}
			}, CancellationToken.None);

			return reactiveResult;
		}
	}
}