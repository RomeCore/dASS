using System.Runtime.CompilerServices;
using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.LLM.MVVM.Additional;
using LLMDesktopAssistant.LLM.MVVM.Additional.Chat;
using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.Utils.Attachments;

namespace LLMDesktopAssistant.Scripting.Lua
{
	/// <summary>
	/// Routes a Lua-side attachable handle to the handler that understands it and remembers what the handle
	/// resolved to, so the same payload can be attached many times and removed again.
	/// </summary>
	[Service]
	public class LuaDataAttachmentDispatcher
	{
		private readonly ILuaDataAttachmentHandler[] _handlers;
		private readonly ConditionalWeakTable<object, AdditionalChatData> _resolved = [];

		/// <summary>
		/// Initializes a new dispatcher over the registered handlers.
		/// </summary>
		/// <param name="handlers">The handlers registered in the container.</param>
		public LuaDataAttachmentDispatcher(IEnumerable<ILuaDataAttachmentHandler> handlers)
		{
			_handlers = [.. handlers.OrderBy(handler => handler.Order)];
		}

		/// <summary>
		/// Resolves the payload behind <paramref name="handle"/>.
		/// </summary>
		/// <remarks>
		/// The payload is built once per handle: attaching the same handle twice shows one object in two
		/// places, which is also what keeps a re-rendered control in sync across its copies.
		/// </remarks>
		/// <param name="handle">The Lua-side handle.</param>
		/// <returns>The payload, or <see langword="null"/> when no handler recognised the handle.</returns>
		public AdditionalChatData? TryResolve(object? handle)
		{
			if (handle is not IChatAttachment attachment)
				return null;

			if (_resolved.TryGetValue(handle, out var resolved))
				return resolved;

			foreach (var handler in _handlers)
			{
				if (handler.TryResolve(attachment) is not { } data)
					continue;

				_resolved.AddOrUpdate(handle, data);
				return data;
			}

			return null;
		}

		/// <summary>
		/// Removes everything the handle produced from the host: the payload and the envelopes wrapping it.
		/// </summary>
		/// <param name="host">The host the payload was attached to.</param>
		/// <param name="handle">The Lua-side handle.</param>
		/// <returns><see langword="true"/> when at least one attached object was removed.</returns>
		public bool TryRemove(IChatAttachmentHost host, object? handle)
		{
			// Only an already resolved handle can be attached: resolving here would build a payload just to remove nothing.
			if (handle is null || !_resolved.TryGetValue(handle, out var payload))
				return false;

			bool removed = false;
			foreach (var attachment in host.GetAttachments().ToList())
			{
				bool matches = attachment is AdditionalChatDataItemBase envelope
					? ReferenceEquals(envelope.Content, payload)
					: ReferenceEquals(attachment, payload);

				if (matches)
					removed |= host.RemoveAttachment(attachment);
			}

			return removed;
		}

		/// <summary>
		/// Creates the error a call gets when its handle resolved to nothing.
		/// </summary>
		/// <param name="handle">The value the script passed.</param>
		/// <param name="call">The Lua call being executed.</param>
		public LuaRuntimeException CreateError(object? handle, string call)
		{
			var actual = handle switch
			{
				null or LuaNil => "nil",
				LuaString => "string",
				LuaNumber => "number",
				LuaTable => "table",
				LuaBoolean => "boolean",
				_ => handle.GetType().Name,
			};

			return new LuaRuntimeException(
				$"{call}: cannot attach a '{actual}' value. " +
				"See manuals(dass.ui, dass.tool.result) for the attachable handles.");
		}
	}
}
