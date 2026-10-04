using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.LLM.MVVM.Additional;
using LLMDesktopAssistant.Tools;
using Material.Icons;

namespace LLMDesktopAssistant.Scripting.Lua.API
{
	/// <summary>
	/// Lua API for reactive tool result: <c>dass.tool.result.*</c>.
	/// Provides access to the current tool's <see cref="ReactiveToolResult"/>
	/// for streaming output, progress updates, status icons, and completion control.
	/// </summary>
	[LuaApi(chatScoped: false)]
	public class LuaApiToolResult : LuaApiBase
	{
		public override string? Namespace => "dass.tool.result";

		public override string? Manuals => """
			--- dass.tool.result — reactive tool result API

			Provides access to the current tool's reactive result for streaming
			output, progress updates, and completion control.
			Available only inside Lua scripts executed as tools or meta-tools.

			FUNCTIONS:
			
			--- dass.tool.result.available()
			  Checks whether a tool execution context is available (i.e. we are inside a running tool).
			  Returns: boolean — true if dass.tool.result functions can be used
			
			--- dass.tool.result.get()
			  Returns the current reactive result object (table), or nil if not in a tool context.
			  The returned table has fields (read-only):
			    - content: string — current accumulated output
			    - is_completed: boolean — whether execution has finished
			    - success: boolean or nil — result status (nil if not completed)
			    - progress: number or nil — current progress (nil = indeterminate)
			    - progress_min: number — minimum progress value (default 0.0)
			    - progress_max: number — maximum progress value (default 1.0)
			    - status_icon: string or nil — current status icon name
			    - status_title: string or nil — current status title
			    - use_markdown: boolean — whether content is rendered as Markdown

			--- dass.tool.result.write(line)
			  Appends a line of text to the result output.
			  Parameters:
			    - line: string — line to append

			--- dass.tool.result.write_lines(lines)
			  Appends multiple lines to the result output at once.
			  Parameters:
			    - lines: table — array of strings

			--- dass.tool.result.set_content(text)
			  Replaces the entire result content with the given text.
			  Parameters:
			    - text: string — new content

			--- dass.tool.result.set_progress(value, [min], [max])
			  Sets the progress of the tool execution with optional range limits.
			  Parameters:
			    - value: number or nil — progress value, or nil for indeterminate
			    - min: number (optional) — minimum progress value (default: 0.0)
			    - max: number (optional) — maximum progress value (default: 1.0)

			--- dass.tool.result.get_progress()
			  Returns the current progress value.
			  Returns: table with fields:
			    - value: number or nil — current progress (nil = indeterminate)
			    - min: number — minimum progress value
			    - max: number — maximum progress value

			--- dass.tool.result.set_status(icon, title)
			  Sets the status icon and title shown in the UI next to the tool name.
			  Parameters:
			    - icon: string — MaterialIconKind name (e.g. "File", "Web", "Check", "Download")
			      Pass empty string or nil to keep current icon.
			    - title: string — status title text (e.g. "Downloading file...")
			      Pass empty string or nil to keep current title.

			--- dass.tool.result.set_structured(data)
			  Sets the structured result data (Lua table → JSON).
			  Parameters:
			    - data: table — structured data to return alongside text content

			--- dass.tool.result.append_data(...)
			  Appends custom UI to the tool result. Each argument must be a 'uicontrol' UserData
			  created by dass.ui.create_control(). One UI element is appended per argument, in order.
			  Appending the same control more than once is allowed.
			  Parameters:
				- ...uicontrol — one or more control handles
			  Returns: nil
			  No-op if not in a tool execution context.

			--- dass.tool.result.use_markdown(enabled)
			  Sets whether the result content should be rendered as Markdown.
			  Parameters:
			    - enabled: boolean — true to enable Markdown rendering

			--- dass.tool.result.complete([success])
			  Completes the tool execution. After calling this, further writes are ignored.
			  Parameters:
			    - success: boolean (optional) — true for success (default), false for error

			--- dass.tool.result.complete_with_success()
			  Completes the tool execution with success status.

			--- dass.tool.result.complete_with_error()
			  Completes the tool execution with error status.

			--- dass.tool.result.clear()
			  Clears all result content lines.

			EXAMPLES:

			  -- Streaming output with progress
			  dass.tool.result.set_status("Download", "Downloading...")
			  dass.tool.result.set_progress(0.0)
			  dass.tool.result.write("Starting download...")
			  -- ... do work ...
			  dass.tool.result.set_progress(0.5)
			  dass.tool.result.write("Halfway there!")
			  -- ... more work ...
			  dass.tool.result.set_progress(1.0)
			  dass.tool.result.write("Done!")
			  dass.tool.result.complete_with_success()

			  -- Structured result
			  dass.tool.result.set_structured({
			    result = "ok",
			    items = { 1, 2, 3 },
			    metadata = {
			      source = "lua",
			      version = 2
			    }
			  })
			  dass.tool.result.complete(true)

			  -- Error case
			  dass.tool.result.set_status("Alert", "Something went wrong")
			  dass.tool.result.write("Error: " .. tostring(err))
			  dass.tool.result.complete_with_error()

			NOTES:
			  - All functions are no-ops if not in a tool execution context.
			  - After complete(), most setters become ineffective.
			  - Use icon.exists() and icon.list() to explore available icon names.
			""";

		public override Action? Populate(LuaTable globals, LuaTable ns, LuaService luaService)
		{
			ns["available"] = new LuaCallbackFunction(Available);
			ns["get"] = new LuaCallbackFunction(Get);
			ns["write"] = new LuaCallbackFunction(Write);
			ns["write_lines"] = new LuaCallbackFunction(WriteLines);
			ns["set_content"] = new LuaCallbackFunction(SetContent);
			ns["set_progress"] = new LuaCallbackFunction(SetProgress);
			ns["get_progress"] = new LuaCallbackFunction(GetProgress);
			ns["set_status"] = new LuaCallbackFunction(SetStatus);
			ns["set_structured"] = new LuaCallbackFunction(SetStructured);
			ns["append_data"] = new LuaCallbackFunction(AppendData);
			ns["use_markdown"] = new LuaCallbackFunction(UseMarkdown);
			ns["complete"] = new LuaCallbackFunction(Complete);
			ns["complete_with_success"] = new LuaCallbackFunction(CompleteWithSuccess);
			ns["complete_with_error"] = new LuaCallbackFunction(CompleteWithError);
			ns["clear"] = new LuaCallbackFunction(Clear);
			return null;
		}

		private static ReactiveToolResult? GetResult(LuaCallingContext ctx)
		{
			return ctx.TryGetReactiveToolResult();
		}

		private LuaTuple Available(LuaCallingContext ctx, LuaValue[] args)
		{
			return new LuaTuple(LuaBoolean.FromBoolean(GetResult(ctx) != null));
		}

		private LuaTuple Get(LuaCallingContext ctx, LuaValue[] args)
		{
			var result = GetResult(ctx);
			if (result == null)
				return new LuaTuple(LuaNil.Instance);

			var table = new LuaTable();

			table["content"] = new LuaString(result.ResultContent);
			table["is_completed"] = LuaBoolean.FromBoolean(result.Completion.IsCompleted);
			table["success"] = result.Completion.IsCompleted
				? LuaBoolean.FromBoolean(result.Completion.Result) as LuaValue
				: LuaNil.Instance;
			table["progress"] = result.Progress.HasValue
				? new LuaNumber(result.Progress.Value) as LuaValue
				: LuaNil.Instance;
			table["progress_min"] = new LuaNumber(result.MinProgress);
			table["progress_max"] = new LuaNumber(result.MaxProgress);
			if (result.StatusIcon != null)
				table["status_icon"] = new LuaString(result.StatusIcon.ToString()!);
			if (result.StatusTitle != null)
				table["status_title"] = new LuaString(result.StatusTitle);
			table["use_markdown"] = LuaBoolean.FromBoolean(result.UseMarkdown);

			return new LuaTuple(table);
		}

		private LuaTuple Write(LuaCallingContext ctx, LuaValue[] args)
		{
			var result = GetResult(ctx);
			if (args.Length > 0 && args[0] is LuaString text && result != null)
				result.ResultContentLines.Add(text.Value);
			return new LuaTuple(LuaNil.Instance);
		}

		private LuaTuple WriteLines(LuaCallingContext ctx, LuaValue[] args)
		{
			var result = GetResult(ctx);
			if (result == null)
				return new LuaTuple(LuaNil.Instance);

			if (args.Length > 0 && args[0] is LuaTable lines)
			{
				foreach (var value in lines.Values)
				{
					if (value is LuaString text)
						result.ResultContentLines.Add(text.Value);
				}
			}

			return new LuaTuple(LuaNil.Instance);
		}

		private LuaTuple SetContent(LuaCallingContext ctx, LuaValue[] args)
		{
			var result = GetResult(ctx);
			if (args.Length > 0 && args[0] is LuaString text && result != null)
				result.ResultContent = text.Value;
			return new LuaTuple(LuaNil.Instance);
		}

		private LuaTuple SetProgress(LuaCallingContext ctx, LuaValue[] args)
		{
			var result = GetResult(ctx);
			if (result == null)
				return new LuaTuple(LuaNil.Instance);

			if (args.Length > 0)
			{
				var value = args[0];
				if (value is LuaNumber num)
					result.Progress = num.Value;
				else if (value is LuaNil)
					result.Progress = null;
			}

			if (args.Length >= 2 && args[1] is LuaNumber minNum)
				result.MinProgress = minNum.Value;
			if (args.Length >= 3 && args[2] is LuaNumber maxNum)
				result.MaxProgress = maxNum.Value;

			return new LuaTuple(LuaNil.Instance);
		}

		private LuaTuple GetProgress(LuaCallingContext ctx, LuaValue[] args)
		{
			var result = GetResult(ctx);
			if (result == null)
				return new LuaTuple(LuaNil.Instance);

			var table = new LuaTable();
			table["value"] = result.Progress.HasValue
				? new LuaNumber(result.Progress.Value) as LuaValue
				: LuaNil.Instance;
			table["min"] = new LuaNumber(result.MinProgress);
			table["max"] = new LuaNumber(result.MaxProgress);
			return new LuaTuple(table);
		}

		private LuaTuple SetStatus(LuaCallingContext ctx, LuaValue[] args)
		{
			var result = GetResult(ctx);
			if (result == null)
				return new LuaTuple(LuaNil.Instance);

			if (args.Length > 0 && args[0] is LuaString iconArg && !string.IsNullOrEmpty(iconArg.Value))
			{
				try
				{
					var icon = Enum.Parse<MaterialIconKind>(iconArg.Value, ignoreCase: true);
					result.StatusIcon = icon;
				}
				catch
				{
					result.StatusIcon = null;
				}
			}

			if (args.Length > 1 && args[1] is LuaString titleArg && !string.IsNullOrEmpty(titleArg.Value))
				result.StatusTitle = titleArg.Value;

			return new LuaTuple(LuaNil.Instance);
		}

		private LuaTuple SetStructured(LuaCallingContext ctx, LuaValue[] args)
		{
			var result = GetResult(ctx);
			if (result == null)
				return new LuaTuple(LuaNil.Instance);

			if (args.Length > 0)
			{
				var data = args[0];
				if (data is LuaTable || data is LuaNil)
				{
					var jsonNode = StructuredLuaConverter.LuaValueToJsonNode(data);
					result.StructuredResult = jsonNode;
				}
			}

			return new LuaTuple(LuaNil.Instance);
		}

		private LuaTuple AppendData(LuaCallingContext ctx, LuaValue[] args)
		{
			var result = GetResult(ctx);
			if (result == null)
				return new LuaTuple(LuaNil.Instance);

			foreach (var arg in args)
			{
				if (arg is not LuaUserData { Target: LuaUiControl uiControl })
					throw new LuaRuntimeException("dass.tool.result.append_data(...): arguments must be 'uicontrol' userdata created by dass.ui.create_control().");

				var additional = new DynamicAxamlControlAdditionalData(uiControl.Xaml, uiControl.ViewModel);

				// Follow the handle: ui:set_xaml(xaml) re-renders every control appended from it.
				void OnXamlChanged(object? _, EventArgs __) => additional.Axaml = uiControl.Xaml;
				uiControl.XamlChanged += OnXamlChanged;
				additional.OnDispose += (_, _) => uiControl.XamlChanged -= OnXamlChanged;

				result.AdditionalData.Add(additional);
			}

			return new LuaTuple(LuaNil.Instance);
		}

		private LuaTuple UseMarkdown(LuaCallingContext ctx, LuaValue[] args)
		{
			var result = GetResult(ctx);
			if (result == null)
				return new LuaTuple(LuaNil.Instance);

			if (args.Length > 0)
				result.UseMarkdown = args[0] is LuaBoolean bVal && bVal.Value;

			return new LuaTuple(LuaNil.Instance);
		}

		private LuaTuple Complete(LuaCallingContext ctx, LuaValue[] args)
		{
			var result = GetResult(ctx);
			if (result == null)
				return new LuaTuple(LuaNil.Instance);

			var success = args.Length == 0 || args[0] is LuaNil || (args[0] is LuaBoolean bVal && bVal.Value);
			result.TryComplete(success);

			return new LuaTuple(LuaNil.Instance);
		}

		private LuaTuple CompleteWithSuccess(LuaCallingContext ctx, LuaValue[] args)
		{
			var result = GetResult(ctx);
			result?.TryCompleteWithSuccess();
			return new LuaTuple(LuaNil.Instance);
		}

		private LuaTuple CompleteWithError(LuaCallingContext ctx, LuaValue[] args)
		{
			var result = GetResult(ctx);
			result?.TryCompleteWithError();
			return new LuaTuple(LuaNil.Instance);
		}

		private LuaTuple Clear(LuaCallingContext ctx, LuaValue[] args)
		{
			var result = GetResult(ctx);
			if (result != null)
				result.ResultContentLines.Clear();
			return new LuaTuple(LuaNil.Instance);
		}
	}
}
