using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.Agents;
using LLMDesktopAssistant.LLM.Services.Agents;
using LLMDesktopAssistant.Prompting.Context.Notifications;

namespace LLMDesktopAssistant.Scripting.Lua.API
{
	/// <summary>
	/// Lua API for prompt notifications: <c>dass.notification.*</c>.
	/// </summary>
	/// <remarks>
	/// A thin producer in front of <see cref="IPromptNotificationManager"/>: the delivery itself is done by the
	/// prompt composer, which turns the pool difference into a <c>&lt;notification&gt;</c> block, so nothing here
	/// touches a message or a turn.
	/// </remarks>
	[LuaApi(chatScoped: true)]
	public class LuaApiNotification(
		IPromptNotificationManager notificationManager,
		IAgentManagementService agentManager
	) : LuaApiBase
	{
		public override string? Namespace => "dass.notification";

		public override string? Manuals => """
			--- dass.notification - prompt notifications

			Pushes a notification into the chat's notification pool. Notifications are not shown to the user:
			they are delivered to the agents' prompts as a <notification> block next to the pending message.

			Delivery is passive - a notification is picked up when an agent's prompt is built. A push made during
			a turn reaches the pushing agent on its next request of the same turn, and every other agent on its
			next turn.

			FUNCTIONS:

			--- dass.notification.push({ content = ..., visible_to = {...}, mode = ... }) -> notification_handle
			  Parameters (one table; only "content" is required):
			    - content: string - the notification text, line breaks allowed. Passed to the model as-is.
			    - visible_to: table (optional) - array of agent names and/or agent ids
			      (see dass.agent.list_chat()). Omitted means every agent of the chat.
			    - mode: string (optional, default "blacklist"):
			        "blacklist" - visible to everyone EXCEPT the agents in visible_to
			        "whitelist" - visible ONLY to the agents in visible_to

			  Returns: notification_handle - see below.

			  Throws when "content" is missing or blank, when an entry of "visible_to" names no agent,
			  or when "mode" is unknown. Names are resolved while push() runs, so a later rename of an agent
			  does not affect the notification that was already pushed.

			NOTIFICATION HANDLE ('notification_handle'):

			  Properties:
			    - cancelled: boolean - true once the notification was cancelled

			  Methods:
			    - h:cancel() -> nil - cancels the notification. It stops being delivered to agents that have
			      not seen it yet; an agent that has already read it keeps it in its history, so cancel cannot
			      retract it. Cancelling twice is a no-op.

			NOTES:
			  - The pool belongs to the current chat and is persisted with it: an unseen notification is still
			    there after the application reopens the chat.
			  - The pool keeps the last 500 notifications, and an agent receives at most the last 25 that
			    arrived since it last pulled, from oldest to newest.
			  - The scripting environment is the same everywhere: a tool, a hook, a slash command and a prompt
			    context all reach this API.
			  - An agent that pushes without "visible_to" receives its own notification on its next request.
			    Use dass.agent.current() with mode = "blacklist" when that is not wanted.

			EXAMPLES:

			  -- Tell every agent that a long build finished
			  dass.notification.push{
			    content = "Build finished: 42 tests passed."
			  }

			  -- Only the reviewer
			  dass.notification.push{
			    content = "Commit " .. sha .. " landed on main.",
			    visible_to = { "Code Reviewer" },
			    mode = "whitelist"
			  }

			  -- Push now, retract later if the condition disappears
			  local handle = dass.notification.push{ content = "Disk almost full", visible_to = { "Ops" } }
			  ...
			  handle:cancel()

			  -- Everyone except the agent running this script
			  local me = dass.agent.current()
			  if me then
			    dass.notification.push{
			      content = me.name .. " is busy, do not wait for it.",
			      visible_to = { me.id },
			      mode = "blacklist"
			    }
			  end
			""";

		public override Action? Populate(LuaTable globals, LuaTable ns, LuaService luaService)
		{
			ns["push"] = new LuaCallbackFunction(Push);
			return null;
		}

		private LuaTuple Push(LuaCallingContext ctx, LuaValue[] args)
		{
			const string call = "dass.notification.push({ content = ..., visible_to = {...}, mode = ... })";

			if (args.Length != 1 || args[0] is not LuaTable options)
				throw new LuaRuntimeException($"{call}: a single table argument is expected.");

			var contentValue = options.Get("content");
			if (contentValue is not LuaString content || string.IsNullOrWhiteSpace(content.Value))
				throw new LuaRuntimeException($"{call}: 'content' is required and must be a non-empty string.");

			var isVisibleToWhiteList = ReadMode(options, call);
			var visibleTo = ReadVisibleTo(options, call);

			var notification = new PromptNotificationItem
			{
				CreatedAt = DateTime.Now,
				Content = content.Value,
				VisibleTo = visibleTo,
				IsVisibleToWhiteList = isVisibleToWhiteList
			};

			notificationManager.Push(notification);

			return new LuaTuple(LuaNotificationHandle.Wrap(new LuaNotificationHandle(notification)));
		}

		/// <summary>
		/// Reads <c>mode</c>. An unknown name is an error rather than a silent blacklist, because a typo in
		/// <c>"whitelist"</c> would otherwise broadcast to everyone.
		/// </summary>
		private static bool ReadMode(LuaTable options, string call)
		{
			var value = options.Get("mode");
			if (value is LuaNil)
				return false;

			if (value is not LuaString mode)
				throw new LuaRuntimeException($"{call}: 'mode' must be a string.");

			return mode.Value.Trim().ToLowerInvariant() switch
			{
				"blacklist" => false,
				"whitelist" => true,
				_ => throw new LuaRuntimeException(
					$"{call}: 'mode' must be 'whitelist' or 'blacklist', got '{mode.Value}'.")
			};
		}

		private List<Guid> ReadVisibleTo(LuaTable options, string call)
		{
			var value = options.Get("visible_to");
			if (value is LuaNil)
				return [];

			if (value is not LuaTable entries)
				throw new LuaRuntimeException($"{call}: 'visible_to' must be an array of agent names or ids.");

			List<Guid> ids = [];
			foreach (var entry in entries.Values)
			{
				if (entry is not LuaString text || string.IsNullOrWhiteSpace(text.Value))
					throw new LuaRuntimeException($"{call}: 'visible_to' must be an array of agent names or ids.");

				ids.Add(ResolveAgentId(text.Value.Trim(), call));
			}

			return ids;
		}

		/// <summary>
		/// Resolves one audience entry to an agent id. A look-alike of a GUID is read as an id, everything else as
		/// a name, so a name can never be shadowed by an agent whose id happens to be spelled the same way.
		/// </summary>
		private Guid ResolveAgentId(string entry, string call)
		{
			var agents = agentManager.ListAgents().ToList();

			if (Guid.TryParse(entry, out var id))
			{
				var agent = agents.FirstOrDefault(a => a.Agent.Id == id).Agent
					?? throw new LuaRuntimeException($"{call}: there is no agent with id '{entry}'.");
				return agent.Id;
			}

			var named = agents
				.FirstOrDefault(a => string.Equals(a.Agent.Info.Name, entry, StringComparison.OrdinalIgnoreCase))
				.Agent
				?? throw new LuaRuntimeException(
					$"{call}: there is no agent named '{entry}'. Use dass.agent.list_chat() to see the names.");

			return named.Id;
		}
	}
}
