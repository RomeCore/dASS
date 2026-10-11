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
	/// <c>chat_message</c> userdata (see <see cref="LuaChatMessage"/>). The array never re-points under the script —
	/// reactivity in Lua would leak side effects with no payoff — while each message handle stays live and writable.
	/// </remarks>
	[LuaApi(chatScoped: true)]
	public class LuaApiChat : LuaApiBase
	{
		public override string? Namespace => "dass.chat";

		public override string? Manuals => """
			--- dass.chat — managing the current chat

			The current chat is the chat the script runs in (this API is chat-scoped).

			FUNCTIONS:

			--- dass.chat.get_messages() -> table
			  Returns a SNAPSHOT array of the active branch's messages, in order.
			  Each entry is a mutable 'chat_message' handle (see below). The array itself
			  does not react to later chat changes; the handles inside it stay live.

			--- dass.chat.get_title() -> string
			  Returns the chat title (read-only in v1).

			--- dass.chat.get_topic() -> string
			  Returns the chat topic (read-only in v1).

			MESSAGE HANDLE ('chat_message'):

			  Properties (read/write):
			    - content: string — the message text
			    - error: string or nil — the message error (locale-rendered on read)
			    - disabled_for_agents: boolean — hide the message from every agent

			  Properties (read-only):
			    - role: string — "user", "assistant", ...
			    - id: number — database message id (-1 if unsaved)
			    - index: number — index in the chat history
			    - branch_index: number — selected branch index
			    - branches_count: number — number of available branches

			  Methods:
			    - msg:append_part(title, icon, color) — a plain chip part
			    - msg:append_content_part(content, title, icon, color) — a part whose text
			      is also appended to the model-facing message content
			    - msg:append_attachment_part(attachment, title, icon, color) — a native
			      attachment part; 'attachment' is an image handle from image.load()/image.create()
			    - msg:append_data(uicontrol, ...) — append one or more UI controls created
			      by dass.ui.create_control()

			  'icon' is a VisualIconKind name (e.g. "Star", "Cards"), 'color' is a hex string
			  (e.g. "#ff88cc"); both are optional (pass nil). Parts are not restorable on edit.

			EXAMPLES:

			  local messages = dass.chat.get_messages()
			  local last = messages[#messages]
			  print(last.role, last.content)
			  last:append_part("handled", "Star", "#ff88cc")
			""";

		private readonly Chat _chat;

		public LuaApiChat(Chat chat)
		{
			_chat = chat;
		}

		public override Action? Populate(LuaTable globals, LuaTable ns, LuaService luaService)
		{
			ns["get_messages"] = new LuaCallbackFunction(GetMessages);
			ns["get_title"] = new LuaCallbackFunction(GetTitle);
			ns["get_topic"] = new LuaCallbackFunction(GetTopic);
			return null;
		}

		private LuaTuple GetMessages(LuaCallingContext ctx, LuaValue[] args)
		{
			var table = new LuaTable();
			var index = 1;
			foreach (var message in _chat.Messages)
				table.Set(index++, LuaChatMessage.Wrap(message));
			return new LuaTuple(table);
		}

		private LuaTuple GetTitle(LuaCallingContext ctx, LuaValue[] args)
			=> new(new LuaString(_chat.Title));

		private LuaTuple GetTopic(LuaCallingContext ctx, LuaValue[] args)
			=> new(new LuaString(_chat.Topic));
	}
}
