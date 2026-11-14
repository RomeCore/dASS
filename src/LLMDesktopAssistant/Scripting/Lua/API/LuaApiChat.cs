using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.LLM.Domain;

namespace LLMDesktopAssistant.Scripting.Lua.API
{
	/// <summary>
	/// Lua API for managing the current chat: <c>dass.chat.*</c>.
	/// The service is chat-scoped, so "the chat" is always the chat the script runs in.
	/// </summary>
	/// <remarks>
	/// <see cref="GetMessages"/> returns a <em>snapshot</em> of the active branch's messages as a plain array of
	/// <c>chat_message</c> userdata (see <see cref="LuaChatMessage"/>). The array never re-points under the script -
	/// reactivity in Lua would leak side effects with no payoff - while each message handle stays live and writable.
	/// </remarks>
	[LuaApi(chatScoped: true)]
	public class LuaApiChat : LuaApiBase
	{
		public override string? Namespace => "dass.chat";

		public override string? Manuals => """
			--- dass.chat - managing the current chat

			The current chat is the chat the script runs in (this API is chat-scoped).

			FUNCTIONS:

			--- dass.chat.get_messages() -> table
			  Returns a SNAPSHOT array of the active branch's messages, in order.
			  Each entry is a mutable 'chat_message' handle (see below). The array itself
			  does not react to later chat changes; the handles inside it stay live.

			--- dass.chat.get_title() -> string
			  Returns the chat title.

			--- dass.chat.get_topic() -> string
			  Returns the chat topic.

			--- dass.chat.append_part(spec) -> chat_part
			  Appends one part - a compact chip with an optional payload - to the CHAT object
			  itself, not to a message.

			  Parameters:
				- spec: table - the part, every field optional:
					title: string - the chip label
					icon: string - an icon name from icon.list()
					color: string - a hex accent color, e.g. "#ff88cc"
					content: string - text this part also contributes to the MODEL-FACING
					  content of the host (the chat, the message or the tool result)
					attachment: handle - a native attachment (e.g. an image from
					  image.load()/image.create())
					visible: boolean - show the chip (default true)
					temporary: boolean - do not persist the part (default false)
					replacement_key: string - update the part already carrying this key

			  'content' and 'attachment' are mutually exclusive: a part is either text or an
			  attachment. A part with neither is a plain chip. The table describes the part
			  WHOLE: a field you leave out gets its default, also when the call updates a part
			  that already exists. An unknown key, a wrong value type or a handle that is not a
			  native attachment is an error - a control or a terminal goes to append_data().

			  Returns: chat_part - the appended part (see below).

			--- dass.chat.append_data(handle, [options])
			  Appends ONE attachable handle to the chat: a 'uicontrol' from dass.ui.create_control(),
			  or a live 'terminal' from process.run_terminal(). The same handle can be appended more
			  than once: every call shows the payload in its own place, while the payload itself is
			  shared, so ui:set_xaml() re-renders every copy of it.

			  Parameters:
			    - handle: uicontrol or terminal - the attachable handle
			    - options: table (optional) - where the payload is shown and how it behaves:
			        surface: string - "rail" puts the payload on the input rail
			        horizontal: string - "left" or "right" (rail only, default "left")
			        vertical: string - "top" or "bottom" (rail only, default "bottom")
			        visible: boolean - show or hide the payload (default true)
					temporary: boolean - do not persist the payload (default false)
					replacement_key: string - replace the item already carrying this key;
					  re-appending the same handle under the same key changes nothing
			      Without a surface the payload is plain additional chat data and lands in the side
			      panel. An unknown key, an unknown value or a rail placement without the rail surface
			      is an error, not a silent fallback.

			  Returns: nil

			--- dass.chat.remove_data(handle) -> boolean
			  Removes everything dass.chat.append_data() attached with this handle: the payload and
			  every rail item wrapping it. A live terminal goes away with its process.
			  Returns: boolean - true when something of this handle was attached to this chat.

			--- dass.chat.on_close(func) -> chat_close_hook
			  Registers func to run when this chat closes: switching to another chat, closing the
			  tab, or shutting the application down.

			  Parameters:
			    - func: function - a plain function, called with no arguments. It cannot await: the
			      close path is synchronous, so a task started here outlives the chat.

			  Returns: chat_close_hook - the registration (see below).

			  Hooks run LIFO, the last registered first, at a point where the chat's services are
			  still alive. An exception in one hook is logged and the rest still run.

			MESSAGE HANDLE ('chat_message'):

			  Properties (read/write):
			    - content: string - the message text
			    - reasoning_signature: string or nil - the reasoning signature string (valid only for assistant messages)
			    - reasoning_content: string or nil - the reasoning message content (valid only for assistant messages)
			    - error: string or nil - the message error (locale-rendered on read)
			    - disabled_for_agents: boolean - hide the message from every agent

			  Properties (read-only):
			    - role: string - "user", "assistant", ...
			    - id: number - database message id (-1 if unsaved)
			    - index: number - index in the chat history (1-based)
			    - branch_index: number - selected branch index (1-based)
			    - branches_count: number - number of available branches
			    - is_user_like: boolean or nil - whether the assistant message pretends to be a user message
			    - usage: chat_message_usage or nil - usage statistics (see below)

			  Methods:
				- msg:append_part(spec) -> chat_part - the same spec table as
				  dass.chat.append_part(); 'content' here reaches the model-facing content of
				  THIS message
			    - msg:append_data(handle, [options]) - append ONE attachable handle: a 'uicontrol'
			      created by dass.ui.create_control(), or a live 'terminal' from
			      process.run_terminal(). Options are the same as for dass.chat.append_data(),
			      except 'surface': the rail exists on the chat only
			    - msg:remove_data(handle) -> boolean - remove everything appended with this handle

			  Parts created from Lua are never restored into the input draft when the message is
			  edited: there is no draft state that produced them.

			PART HANDLE ('chat_part'):

			  Methods:
				- part:remove() -> boolean - detach the part from its host, disposing it (a part
				  can own a native attachment). true when the part was still there; false when it
				  was replaced by 'replacement_key', removed earlier, or its host is gone.
				  Never raises.
			
			MESSAGE USAGE HANDLE ('chat_message_usage'):
			
			  Properties (read-only):
			    - model_name: string - the model that produced the message
			    - input_tokens: number or nil - tokens sent to the model as input
			    - cache_create_tokens: number or nil - input tokens written to the prompt cache
			    - input_cache_hit_tokens: number or nil - input tokens served from the prompt cache
			    - input_cache_miss_tokens: number or nil - input tokens that missed the prompt cache
			    - output_tokens: number or nil - tokens generated by the model
			    - time_to_first_token: number - seconds until the first output token arrived
			    - generation_time: number - seconds spent generating the message
			
			CLOSE HOOK HANDLE ('chat_close_hook'):
			
			  Properties (read-only):
			    - registered: boolean - true while the hook will still run
			
			  Methods:
				- h:cancel() -> boolean - unregisters it; true when it was still registered
			
			NOTES:
			  - The hook runs detached from the cancellation of the call that registered it: a Stop
			    later does not skip it, and a hook cannot cancel the chat close.
			  - A process kill runs nothing. This is a courtesy, not a guarantee.
			  - Scripts reload without closing the chat and a hook survives that reload, so
			    registering unconditionally whenever a script loads makes it run twice. Register from
			    a tool or a command (one call, one hook), or keep a flag in store.session.
			
			EXAMPLES:

			  local messages = dass.chat.get_messages()
			  local last = messages[#messages]
			  print(last.role, last.content)
			  last:append_part{ title = "handled", icon = "Star", color = "#ff88cc" }

			  -- A chip that is also a text contribution, replaced in place while a job runs.
			  local chip = msg:append_part{ title = "Step 1/3", icon = "Download",
				  content = "Reading files...", replacement_key = "job" }
			  msg:append_part{ title = "Step 2/3", icon = "Download", content = "Writing...",
				  replacement_key = "job" } -- the same chip, updated
			  chip:remove() -- nothing to remove any more: it was replaced

			  -- An image as a chip.
			  local image = image.load("shot.png")
			  msg:append_part{ attachment = image, title = "Screenshot" }

			  -- A live terminal can be attached to any host, including the chat.
			  local t = process.run_terminal("claude")
			  if t then dass.chat.append_data(t) end

			  -- A control on the input rail: the right group of the strip above the input.
			  local ui = dass.ui.create_control("...", model)
			  dass.chat.append_data(ui, { surface = "rail", horizontal = "right", vertical = "top" })
			  dass.chat.remove_data(ui) -- takes the rail item and the control with it

			  -- Something small and synchronous: a close hook cannot await
			  local hook = dass.chat.on_close(function()
			    -- remember: no awaits inside a hook!
			    -- it runs in a synchronous context where `await` keyword is not allowed
			    -- (same as addon-based Lua scripts (inside .agents/scripts/lua)).
			    fs.write(".llmassist/notes.md", table.concat(notes, "\n"))
			  end)

			  -- The player changed their mind
			  hook:cancel()
			""";

		private readonly Chat _chat;
		private readonly LuaDataAttachmentDispatcher _dispatcher;
		private readonly LuaChatClosingHandler _closingHandler;

		public LuaApiChat(Chat chat, LuaDataAttachmentDispatcher dispatcher,
			LuaChatClosingHandler closingHandler)
		{
			_chat = chat;
			_dispatcher = dispatcher;
			_closingHandler = closingHandler;
		}

		public override Action? Populate(LuaTable globals, LuaTable ns, LuaService luaService)
		{
			ns["get_messages"] = new LuaCallbackFunction(GetMessages);
			ns["get_title"] = new LuaCallbackFunction(GetTitle);
			ns["get_topic"] = new LuaCallbackFunction(GetTopic);
			ns["append_part"] = new LuaCallbackFunction(AppendPart);
			ns["append_data"] = new LuaCallbackFunction(AppendData);
			ns["remove_data"] = new LuaCallbackFunction(RemoveData);
			ns["on_close"] = new LuaCallbackFunction(OnClose);
			return null;
		}

		private LuaTuple OnClose(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length == 0 || args[0] is not LuaFunction function)
				throw new LuaRuntimeException("dass.chat.on_close(func): a function is expected.");

			return new LuaTuple(LuaCloseHook.Wrap(_closingHandler.Register(function, ctx)));
		}

		private LuaTuple GetMessages(LuaCallingContext ctx, LuaValue[] args)
		{
			var table = new LuaTable();
			var index = 1;
			foreach (var message in _chat.Messages)
				table.Set(index++, LuaChatMessage.Wrap(message, _dispatcher));
			return new LuaTuple(table);
		}

		private LuaTuple GetTitle(LuaCallingContext ctx, LuaValue[] args)
			=> new(new LuaString(_chat.Title));

		private LuaTuple GetTopic(LuaCallingContext ctx, LuaValue[] args)
			=> new(new LuaString(_chat.Topic));

		private LuaTuple AppendPart(LuaCallingContext ctx, LuaValue[] args)
		{
			var part = LuaAttachmentPartAppender.Append(_chat, args.Length > 0 ? args[0] : null,
				"dass.chat.append_part(spec)");

			return new LuaTuple(LuaChatPart.Wrap(new LuaChatPart(_chat, part)));
		}

		private LuaTuple AppendData(LuaCallingContext ctx, LuaValue[] args)
		{
			const string call = "dass.chat.append_data(handle, options)";

			var handle = args.Length > 0 ? Unwrap(args[0]) : null;
			var options = LuaAttachmentDataOptions.Parse(args.Length > 1 ? args[1] : null, call);
			var payload = _dispatcher.TryResolve(handle) ?? throw _dispatcher.CreateError(handle, call);
			var item = options.Apply(payload, isChatHost: true, call);

			// Re-appending a payload under its own key would replace and dispose the live object itself.
			if (!LuaAttachmentDataOptions.IsAlreadyAttached(_chat, item))
				_chat.AddAttachment(item);

			return new LuaTuple(LuaNil.Instance);
		}

		private LuaTuple RemoveData(LuaCallingContext ctx, LuaValue[] args)
		{
			var handle = args.Length > 0 ? Unwrap(args[0]) : null;

			var removed = _dispatcher.TryRemove(_chat, handle);
			return new LuaTuple(removed ? LuaBoolean.True : LuaBoolean.False);
		}

		private static object? Unwrap(LuaValue value)
			=> value is LuaUserData userData ? userData.Target : value;
	}
}
