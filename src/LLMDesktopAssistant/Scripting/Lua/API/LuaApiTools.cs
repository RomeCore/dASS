using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.Services.Tools;
using LLMDesktopAssistant.Tools;
using LLMDesktopAssistant.Tools.Execution.Calls;
using LLMDesktopAssistant.Tools.Execution.Contexts;
using LLMDesktopAssistant.Tools.Execution.Dispatch;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Tools.Execution.Sinks;
using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.Scripting.Lua.API
{
	[LuaApi(chatScoped: true)]
	public class LuaApiTools : LuaApiBase
	{
		public override string? Namespace => "dass.tool";

		public override string? Manuals => """
			--- dass.tool — assistant's tool system

			Provides access to all registered tools (filesystem, web search, Python
			execution, etc.) directly from Lua scripts.

			FUNCTIONS:

			--- async dass.tool.call(name, args)
			  Calls a registered tool by name and returns a structured result table.

			  Parameters:
			    - name: string — tool name (e.g. "web-search", "fs-read_entry")
			    - args: table — arguments to pass to the tool, converted to JSON internally.
			      Use string keys matching the tool's argument schema.
			      Booleans: true / false
			      Arrays: { "a", "b", "c" }
			      Nested objects: { key = { subkey = "value" } }
			      If the tool requires no arguments, pass an empty table {}.

			  Returns: table — structured result with the following fields:
			    - content: string - the textual result produced by the tool
				- structured: table or nil - the optional structured result produced by the tool
			    - success: boolean — whether the tool executed successfully
			    - tool: table — the table containing info of the tool that was called (see dass.tool.list() for what that table contains)
			    - status_title: string or nil — optional status title
			    - status_icon: string or nil — optional status icon name

			  A tool-level failure is not an exception: check `success` and `content`. An error is
			  thrown only when the tool name is not registered. A nested call is a script invocation:
			  it skips policy and confirmation (the script is trusted) and it never asks the user.

			--- dass.tool.list()
			  Returns a table with detailed information about all registered tools.

			  Returns: table — array of tool info tables. Each entry contains:
			    - name: string — tool name (e.g. "web-search")
			    - description: string — tool description
			    - category: string — tool category (e.g. "web", "scripting")
			    - display_name: string or nil — user-friendly name
			    - enabled: boolean or nil — whether the tool is enabled, nil when tool is not changed by settings and default 'enabled' state is used
			    - approval_level: string — the tool's approval policy, in kebab-case. One of:
			        "default" - tool is subject to the agent's default approval level
			        "policy-based" — defer to the agent's behaviour policy (auto-approve, prompt, or reject)
			        "policy-ask-or-disallow" — prompt the user even if the agent auto-approved the behaviour
			        "policy-approve-or-ask" — never disallow; auto-approve or prompt per agent policy
			        "policy-auto-approve-unless-disallowed" — never prompt; execute unless explicitly disallowed
			        "policy-auto-disallow-unless-approved" — never prompt; reject unless explicitly auto-approved
			        "always-approve" — execute immediately, bypassing all policy checks
			        "always-ask" — always prompt the user for confirmation
			        "always-disallow" — always reject without executing
			    - source: string — tool source ("native", "meta", "mcp")
			    - arguments: table — JSON schema of the arguments

			EXAMPLES:

			  -- Flip a coin (structured result)
			  local r = await dass.tool.call("random-coin_flip", {})
			  print("Success:", r.success)
			  print("Result:", r.content)

			  -- Search the web
			  local r = await dass.tool.call("web-search", {
			    query = "weather in London",
			    maxResults = 3
			  })
			  if r.success then
			    print(r.content)
			  end

			  -- Read a file
			  local r = await dass.tool.call("fs-read_entry", {
			    path = "README.md"
			  })
			  print(r.content)

			NOTES:
			- Lua tables are automatically serialized to JSON.
			- Table keys must be strings (quotes optional in Lua,
			  but use ["key-name"] for keys containing spaces or hyphens).
			- The result is a structured table containing content, success status, and metadata.
			""";

		private readonly Chat _chat;
		private readonly IToolsetCacheService _toolsetCache;
		private readonly IToolInvocationDispatcher _dispatcher;

		/// <summary>
		/// A nested call has no owning chat message, so the script entry passes its own context factory
		/// instead of the chat tool's stamped one.
		/// </summary>
		private readonly IToolExecutionContextFactory _contextFactory;

		public LuaApiTools(Chat chat, IToolsetCacheService toolsetCache, IToolInvocationDispatcher dispatcher)
		{
			_chat = chat;
			_toolsetCache = toolsetCache;
			_dispatcher = dispatcher;
			_contextFactory = new ScriptToolContextFactory(_chat);
		}

		public override Action? Populate(LuaTable globals, LuaTable ns, LuaService luaService)
		{
			ns["call"] = new LuaCallbackFunction(Call);
			ns["list"] = new LuaCallbackFunction(ListTools);
			return null;
		}

		public async Task<LuaTuple> Call(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1 || args[0] is not LuaString functionName)
				throw new LuaRuntimeException("dass.tool.call(name, args): first argument must be a string (tool name).");

			if (!_toolsetCache.AvailableTools.TryGetValue(functionName.Value, out var tool))
				throw new LuaRuntimeException($"Tool '{functionName.Value}' not found.");

			// The call is in-memory and belongs to the script, not to the tool the script is running in.
			var call = new ScriptToolCall
			{
				ToolName = tool.Name,
				Arguments = args.Length > 1
					? StructuredLuaConverter.LuaValueToJsonNode(args[1])?.ToJsonString() ?? "{}"
					: "{}"
			};

			await _dispatcher.RunAsync(new ToolInvocationRequest
			{
				Source = ToolInvocationSource.Script,
				Call = call,
				Descriptor = tool,
				Confirmation = ConstantConfirmationSink.Approve,
				ContextFactory = _contextFactory,

				// A nested call is programmatic: there is no surface to render a form on and nobody to
				// answer it. UI-requiring tools must take their non-interactive path instead of hanging.
				RunningInUi = false
			}, ctx.CancellationToken);

			var resultTable = new LuaTable();
			resultTable["content"] = new LuaString(call.ResultContent ?? string.Empty);
			resultTable["success"] = LuaBoolean.FromBoolean(call.Status == ToolStatus.Success);

			if (call.StructuredResult is not null)
				resultTable["structured"] = StructuredLuaConverter.JsonNodeToLuaValue(call.StructuredResult);

			resultTable["tool"] = ToolToTable(tool);

			if (call.StatusTitle is not null)
				resultTable["status_title"] = new LuaString(call.StatusTitle);

			if (call.StatusIcon is { } icon)
				resultTable["status_icon"] = new LuaString(icon.ToString());

			return new LuaTuple(resultTable);
		}

		public LuaTuple ListTools(LuaCallingContext ctx, LuaValue[] args)
		{
			var resultTable = new LuaTable();

			int i = 1;
			foreach (var (_, tool) in _toolsetCache.AvailableTools)
			{
				var entry = ToolToTable(tool);
				resultTable.Set(i++, entry);
			}

			return new LuaTuple(resultTable);
		}

		private static LuaTable ToolToTable(ToolInfo tool)
		{
			var result = new LuaTable();
			result["name"] = new LuaString(tool.Name);
			result["description"] = new LuaString(tool.Description);
			result["category"] = new LuaString(tool.CategoryKey?.Key ?? string.Empty);
			if (tool.NameKey != null)
				result["display_name"] = new LuaString(tool.NameKey.Key);
			result["enabled"] = tool.Enabled is null ? LuaNil.Instance : LuaBoolean.FromBoolean(tool.Enabled.Value);
			result["approval_level"] = tool.ApprovalLevel is null ? new LuaString("default") :
				new LuaString(KebabEnumNames<ToolApprovalLevel>.Serialize(tool.ApprovalLevel.Value) ?? "policy-based");
			result["source"] = new LuaString(tool.ToolSource.ToString().ToLower());
			result["arguments"] = StructuredLuaConverter.JsonNodeToLuaValue(tool.ArgumentSchema);
			return result;
		}
	}
}
