using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.LLM.MVVM.Additional;
using Material.Icons;

namespace LLMDesktopAssistant.Scripting.Lua.API
{
	/// <summary>
	/// Lua API for reactive tool result: <c>dass.tool.result.*</c>.
	/// Provides access to the current tool's result (see <see cref="LuaToolResult"/>)
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
			    - icon: string — VisualIconKind name (e.g. "File", "Web", "Check", "Download")
			      Pass empty string or nil to keep current icon.
			    - title: string — status title text (e.g. "Downloading file...")
			      Pass empty string or nil to keep current title.

			--- dass.tool.result.set_structured(data)
			  Sets the structured result data (Lua table → JSON).
			  Parameters:
			    - data: table — structured data to return alongside text content

			--- dass.tool.result.append_part(title, icon, color)
			  Appends a plain chip part to the tool call. 'icon' is a VisualIconKind name,
			  'color' is a hex string; both are optional (pass nil).

			--- dass.tool.result.append_content_part(content, title, icon, color)
			  Appends a part whose text is also appended to the model-facing tool result.

			--- dass.tool.result.append_attachment_part(attachment, title, icon, color)
			  Appends a native attachment part. 'attachment' is any handle that implements
			  ILuaNativeAttachable (e.g. an image handle from image.load()/image.create()).

			--- dass.tool.result.append_data(...)
			  Appends one or more attachable handles to the tool result: a 'uicontrol' created by
			  dass.ui.create_control(), or a live 'terminal' from process.run_terminal().
			  Each argument is attached in order; appending the same handle more than once is allowed.
			  Parameters:
				- ...handle — one or more attachable handles
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
			  Clears the result content AND every attachment of the call, disposing them.
			  Removing a terminal attachment kills the process behind it.

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

		private readonly LuaDataAttachmentDispatcher _dispatcher;

		/// <summary>
		/// Initializes a new instance of the <see cref="LuaApiToolResult"/> class.
		/// </summary>
		/// <param name="dispatcher">The attachment dispatcher used by <c>append_data</c>.</param>
		public LuaApiToolResult(LuaDataAttachmentDispatcher dispatcher)
		{
			_dispatcher = dispatcher;
		}

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
			ns["append_part"] = new LuaCallbackFunction(AppendPart);
			ns["append_content_part"] = new LuaCallbackFunction(AppendContentPart);
			ns["append_attachment_part"] = new LuaCallbackFunction(AppendAttachmentPart);
			ns["append_data"] = new LuaCallbackFunction(AppendData);
			ns["use_markdown"] = new LuaCallbackFunction(UseMarkdown);
			ns["complete"] = new LuaCallbackFunction(Complete);
			ns["complete_with_success"] = new LuaCallbackFunction(CompleteWithSuccess);
			ns["complete_with_error"] = new LuaCallbackFunction(CompleteWithError);
			ns["clear"] = new LuaCallbackFunction(Clear);
			return null;
		}

		private static LuaToolResult? GetResult(LuaCallingContext ctx)
		{
			return LuaExecutionContext.Result;
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

			table["content"] = new LuaString(result.Content);
			table["is_completed"] = LuaBoolean.FromBoolean(result.IsCompleted);
			table["success"] = result.IsCompleted
				? LuaBoolean.FromBoolean(result.Success == true) as LuaValue
				: LuaNil.Instance;
			table["progress"] = result.Progress.HasValue
				? new LuaNumber(result.Progress.Value) as LuaValue
				: LuaNil.Instance;
			table["progress_min"] = new LuaNumber(result.ProgressMin);
			table["progress_max"] = new LuaNumber(result.ProgressMax);
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
				result.Write(text.Value);
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
						result.Write(text.Value);
				}
			}

			return new LuaTuple(LuaNil.Instance);
		}

		private LuaTuple SetContent(LuaCallingContext ctx, LuaValue[] args)
		{
			var result = GetResult(ctx);
			if (args.Length > 0 && args[0] is LuaString text && result != null)
				result.SetContent(text.Value);
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
					result.SetProgress(num.Value);
				else if (value is LuaNil)
					result.SetProgress(null);
			}

			if (args.Length >= 2 && args[1] is LuaNumber minNum)
				result.SetProgress(result.Progress, minNum.Value);
			if (args.Length >= 3 && args[2] is LuaNumber maxNum)
				result.SetProgress(result.Progress, null, maxNum.Value);

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
			table["min"] = new LuaNumber(result.ProgressMin);
			table["max"] = new LuaNumber(result.ProgressMax);
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
					var icon = VisualIconKind.Parse(iconArg.Value);
					result.SetStatusIcon(VisualIconDataHandler.Resolve(icon).IsOk ? icon : null);
				}
				catch
				{
					result.SetStatusIcon(null);
				}
			}

			if (args.Length > 1 && args[1] is LuaString titleArg && !string.IsNullOrEmpty(titleArg.Value))
				result.SetStatusTitle(titleArg.Value);

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
					result.SetStructured(jsonNode);
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
				_dispatcher.Attach(result.Host, Unwrap(arg), "dass.tool.result.append_data(...)");

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
			result.Complete(success);

			return new LuaTuple(LuaNil.Instance);
		}

		private LuaTuple CompleteWithSuccess(LuaCallingContext ctx, LuaValue[] args)
		{
			var result = GetResult(ctx);
			result?.Complete(true);
			return new LuaTuple(LuaNil.Instance);
		}

		private LuaTuple CompleteWithError(LuaCallingContext ctx, LuaValue[] args)
		{
			var result = GetResult(ctx);
			result?.Complete(false);
			return new LuaTuple(LuaNil.Instance);
		}

		private LuaTuple Clear(LuaCallingContext ctx, LuaValue[] args)
		{
			var result = GetResult(ctx);
			if (result is null)
				return new LuaTuple(LuaNil.Instance);

			result.Clear();
			result.Host.ClearAttachments();
			return new LuaTuple(LuaNil.Instance);
		}

		private LuaTuple AppendPart(LuaCallingContext ctx, LuaValue[] args)
		{
			var host = GetResult(ctx)?.Host;
			host?.AddAttachment(LuaAttachmentParts.CreatePart(Str(args, 0), Str(args, 1), Str(args, 2)));
			return new LuaTuple(LuaNil.Instance);
		}

		private LuaTuple AppendContentPart(LuaCallingContext ctx, LuaValue[] args)
		{
			var host = GetResult(ctx)?.Host;
			host?.AddAttachment(LuaAttachmentParts.CreateContentPart(Str(args, 0), Str(args, 1), Str(args, 2), Str(args, 3)));
			return new LuaTuple(LuaNil.Instance);
		}

		private LuaTuple AppendAttachmentPart(LuaCallingContext ctx, LuaValue[] args)
		{
			var host = GetResult(ctx)?.Host;
			if (host is null)
				return new LuaTuple(LuaNil.Instance);

			if (args.Length == 0 || Unwrap(args[0]) is not ILuaNativeAttachable attachable)
				throw new LuaRuntimeException(
					"dass.tool.result.append_attachment_part(attachment, ...): 'attachment' must implement " +
					"ILuaNativeAttachable (e.g. an image handle created by image.load()/image.create()).");

			host.AddAttachment(LuaAttachmentParts.CreateNativePart(
				attachable.GetNativeAttachment(), Str(args, 1), Str(args, 2), Str(args, 3)));
			return new LuaTuple(LuaNil.Instance);
		}

		private static string? Str(LuaValue[] args, int index)
			=> args.Length > index && args[index] is LuaString value ? value.Value : null;

		private static object? Unwrap(LuaValue value)
			=> value is LuaUserData userData ? userData.Target : value;
	}
}
