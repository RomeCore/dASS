using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.Utils.Attachments;

namespace LLMDesktopAssistant.Scripting.Lua
{
	/// <summary>
	/// Routes a Lua-side attachable handle to the handler that understands it.
	/// </summary>
	[Service]
	public class LuaDataAttachmentDispatcher
	{
		private readonly ILuaDataAttachmentHandler[] _handlers;

		/// <summary>
		/// Initializes a new dispatcher over the registered handlers.
		/// </summary>
		/// <param name="handlers">The handlers registered in the container.</param>
		public LuaDataAttachmentDispatcher(IEnumerable<ILuaDataAttachmentHandler> handlers)
		{
			_handlers = [.. handlers.OrderBy(handler => handler.Order)];
		}

		/// <summary>
		/// Attaches the payload behind <paramref name="handle"/> to <paramref name="host"/>.
		/// </summary>
		/// <param name="host">The host the payload is attached to.</param>
		/// <param name="handle">The Lua-side handle.</param>
		/// <param name="functionName">The Lua function being executed, for the error message.</param>
		/// <exception cref="LuaRuntimeException">No handler recognised the handle.</exception>
		public void Attach(IChatAttachmentHost host, object? handle, string functionName)
		{
			if (handle is IChatAttachment attachment)
			{
				foreach (var handler in _handlers)
				{
					if (handler.TryAttach(host, attachment))
						return;
				}
			}

			var actual = handle switch
			{
				null => "nil",
				LuaString => "string",
				LuaNumber => "number",
				LuaTable => "table",
				LuaBoolean => "boolean",
				_ => handle.GetType().Name,
			};
			throw new LuaRuntimeException(
				$"{functionName}: cannot attach a '{actual}' value. " +
				"See manuals(dass.ui, dass.tool.result) for the attachable handles.");
		}
	}
}
