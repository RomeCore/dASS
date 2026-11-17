using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.Agents;
using LLMDesktopAssistant.LLM.Services.Agents;

namespace LLMDesktopAssistant.Scripting.Lua.API
{
	/// <summary>
	/// The inspection half of the <c>dass.agent</c> namespace: which chat agents exist and who runs the current
	/// script.
	/// </summary>
	/// <remarks>
	/// Task launching lives in <see cref="LuaApiAgentsExecution"/>; this part never touches the task executor.
	/// </remarks>
	[LuaApi(chatScoped: true)]
	public class LuaApiAgents(IAgentManagementService agentManager) : LuaApiBase
	{
		public override string? Namespace => "dass.agent";

		public override string? Manuals => """
			--- dass.agent - chat agent inspection

			Lists the chat agents and reports which one the running script belongs to. Launching an agentic task is
			`dass.agent.execute_task`, documented separately in this namespace's manuals.

			FUNCTIONS:

			--- dass.agent.list_chat([scope]) -> table
			  Returns an array of chat agents (see AGENT TABLE below). Only chat agents are listed: the agents
			  defined in this chat plus the global agents.

			  Parameters:
			    - scope: string - which agents to return, default "all":
			      "all"    - the agents defined in this chat plus the global agents
			      "chat"   - only the agents defined in this chat
			      "global" - only the global agents

			  Throws when the scope is unknown.

			--- dass.agent.get_chat(name_or_id) -> table or nil
			  Finds one chat agent by name or by id. An agent defined in this chat wins over a global agent of
			  the same name; among equal names the first listed one wins. Returns nil when there is no such agent.

			--- dass.agent.current() -> table or nil
			  The agent whose flow the running script belongs to: the agent that is generating, calling a tool,
			  running a slash command or building a prompt context. Returns nil when the script runs outside
			  such a flow.

			AGENT TABLE:

			  Properties (read-only):
				- id: string - the agent's GUID, the same value dass.notification accepts in visible_to
				- name: string - the display name
				- description: string - what the agent says about itself
				- is_global: boolean - defined for the whole application rather than for this chat
				- custom_model: string - the agent's own model in "Provider$Model" form, empty when it follows
				  the chat's model
				- identify_as_user: boolean - this agent's messages are treated as user messages

			EXAMPLES:

			  -- Everything the chat can use, with its origin
			  for _, agent in ipairs(dass.agent.list_chat()) do
			    print(agent.name, agent.is_global and "global" or "chat")
			  end

			  -- The chat's own agents only
			  for _, agent in ipairs(dass.agent.list_chat("chat")) do
			    print(agent.id, agent.name)
			  end

			  -- Who is running this script
			  local me = dass.agent.current()
			  if me then dass.log.info("script runs for " .. me.name) end

			NOTES:
			  - The list is a snapshot: a later settings change is not reflected in a table you already hold.
			  - Sub-agents (the addon-backed agents an agent reaches with agent-callsub) are a different kind
			    and are not listed here.
			  - Agentic tasks are not listed either: the handle `execute_task` returns is the only door to one.
			""";

		public override Action? Populate(LuaTable globals, LuaTable ns, LuaService luaService)
		{
			ns["list_chat"] = new LuaCallbackFunction(ListChat);
			ns["get_chat"] = new LuaCallbackFunction(GetChat);
			ns["current"] = new LuaCallbackFunction(Current);
			return null;
		}

		private LuaTuple ListChat(LuaCallingContext ctx, LuaValue[] args)
		{
			var scope = args.Length > 0 && args[0] is LuaString scopeValue
				? scopeValue.Value.Trim().ToLowerInvariant()
				: "all";

			var agents = scope switch
			{
				"all" or "" => agentManager.ListAgents(),
				"chat" => agentManager.ListAgents().Where(a => !a.IsGlobal),
				"global" => agentManager.ListAgents().Where(a => a.IsGlobal),
				_ => throw new LuaRuntimeException(
					"dass.agent.list_chat([scope]): unknown scope '" + scope + "'. Expected one of: all, chat, global.")
			};

			var table = LuaExtensions.CreateArrayTable();
			var index = 1;
			foreach (var (agent, isGlobal) in agents)
				table.Set(index++, ToAgentTable(agent, isGlobal));

			return new LuaTuple(table);
		}

		private LuaTuple GetChat(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length == 0 || args[0] is not LuaString identifier || string.IsNullOrWhiteSpace(identifier.Value))
				throw new LuaRuntimeException("dass.agent.get_chat(name_or_id): a name or id string is expected.");

			var value = identifier.Value.Trim();

			// ListAgents() yields the chat's own agents first, so an equal name resolves to the chat agent over the global one.
			var agents = agentManager.ListAgents().ToList();
			var match = Guid.TryParse(value, out var id)
				? agents.FirstOrDefault(a => a.Agent.Id == id)
				: agents.FirstOrDefault(a => string.Equals(a.Agent.Info.Name, value, StringComparison.OrdinalIgnoreCase));

			return new LuaTuple(match.Agent is null ? LuaNil.Instance : ToAgentTable(match.Agent, match.IsGlobal));
		}

		private LuaTuple Current(LuaCallingContext ctx, LuaValue[] args)
		{
			var agent = LuaExecutionContext.Agent;
			if (agent is null)
				return new LuaTuple(LuaNil.Instance);

			var isGlobal = agentManager.ListAgents().Any(a => a.Agent.Id == agent.Id && a.IsGlobal);
			return new LuaTuple(ToAgentTable(agent, isGlobal));
		}

		/// <summary>
		/// Shapes one chat agent for Lua. The profile image is deliberately absent - a base64 blob per agent would
		/// dominate the table for no scripting use.
		/// </summary>
		private static LuaTable ToAgentTable(ChatAgentDescriptor agent, bool isGlobal)
		{
			var table = new LuaTable();
			table["id"] = new LuaString(agent.Id.ToString());
			table["name"] = new LuaString(agent.Info.Name);
			table["description"] = new LuaString(agent.Info.Description);
			table["is_global"] = LuaBoolean.FromBoolean(isGlobal);
			table["custom_model"] = new LuaString(agent.Info.CustomModel);
			table["identify_as_user"] = LuaBoolean.FromBoolean(agent.Info.IdentifyAsUser);
			return table;
		}
	}
}
// 666 строчек епта (уже неактуально, но пусть будет)
// UPD (17.11.2026): пиздец как неактуально уже...
