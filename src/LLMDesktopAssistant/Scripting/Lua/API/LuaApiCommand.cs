using AsyncLua;
using AsyncLua.Values;

namespace LLMDesktopAssistant.Scripting.Lua.API
{
	/// <summary>
	/// Lua API for the running slash command: <c>dass.command.*</c>.
	/// The service is chat-scoped, but the context it exposes only exists while a command script runs.
	/// </summary>
	[LuaApi(chatScoped: true)]
	public class LuaApiCommand : LuaApiBase
	{
		public override string? Namespace => "dass.command";

		public override string? Manuals => """
			--- dass.command — the running slash command

			Available only inside a script executed as a slash command.

			FUNCTIONS:

			--- dass.command.context.get_message() -> chat_message or nil
			  Returns the handle of the message the command was invoked on (see dass.chat
			  for the message surface), or nil when not running inside a command.

			EXAMPLES:

			  local msg = dass.command.context.get_message()
			  if msg then msg:append_part("done", "Check", "#88ff88") end
			""";

		private readonly LuaDataAttachmentDispatcher _dispatcher;

		public LuaApiCommand(LuaDataAttachmentDispatcher dispatcher)
		{
			_dispatcher = dispatcher;
		}

		public override Action? Populate(LuaTable globals, LuaTable ns, LuaService luaService)
		{
			var context = new LuaTable();
			context["get_message"] = new LuaCallbackFunction(GetMessage);
			ns["context"] = context;
			return null;
		}

		private LuaTuple GetMessage(LuaCallingContext ctx, LuaValue[] args)
		{
			var command = LuaExecutionContext.Command;
			if (command is null)
				return new LuaTuple(LuaNil.Instance);

			// The context carries the ChatMessage; the handle wraps the BranchedMessage that owns it.
			var branched = command.Chat.Messages.FirstOrDefault(m => ReferenceEquals(m.Message, command.Message));
			if (branched is null)
				return new LuaTuple(LuaNil.Instance);

			return new LuaTuple(LuaChatMessage.Wrap(branched, _dispatcher));
		}
	}
}
