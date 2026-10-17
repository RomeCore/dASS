using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.Services.Tools;
using LLMDesktopAssistant.Tools;
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

			  Throws an error if the tool name is not found or execution fails.
			  Use pcall() for safe error handling.

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
		
		// Kept for the P4 dispatcher: a nested call needs the chat scope to build its own context.
		private readonly Chat _chat;
		private readonly IToolsetCacheService _toolsetCache;

		public LuaApiTools(Chat chat, IToolsetCacheService toolsetCache)
		{
			_chat = chat;
			_toolsetCache = toolsetCache;
		}

		public override Action? Populate(LuaTable globals, LuaTable ns, LuaService luaService)
		{
			ns["call"] = new LuaCallbackFunction(Call);
			ns["list"] = new LuaCallbackFunction(ListTools);
			return null;
		}

		public Task<LuaTuple> Call(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1 || args[0] is not LuaString functionName)
				throw new LuaRuntimeException("dass.tool.call(name, args): first argument must be a string (tool name).");

			if (!_toolsetCache.AvailableTools.ContainsKey(functionName.Value))
				throw new LuaRuntimeException($"Tool '{functionName.Value}' not found.");

			// TODO(P4): run the nested call through the tool invocation dispatcher. The tool has to execute
			// against its own call (ToolInvocationSource.Script) so that it does not overwrite the call of the
			// script that is asking; no IToolCall implementation exists yet, so the context cannot be built here.
			throw new LuaRuntimeException(
				"dass.tool.call is unavailable until the tool invocation dispatcher lands (P4).");
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
