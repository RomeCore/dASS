using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.LLM.MVVM.Additional;
using LLMDesktopAssistant.Utils.Attachments;

namespace LLMDesktopAssistant.Scripting.Lua
{
	/// <summary>
	/// A handle over one part a script appended, exposed to Lua as the <c>chat_part</c> userdata.
	/// </summary>
	/// <remarks>
	/// The handle carries the host it was appended to together with the part itself, so <c>remove()</c> needs no
	/// search. It dies with the Lua state, and a part replaced by <c>replacement_key</c> leaves a stale handle that
	/// removes nothing - which <see cref="Remove"/> reports as <see langword="false"/> rather than guessing.
	/// </remarks>
	public sealed class LuaChatPart
	{
		private readonly IChatAttachmentHost _host;
		private readonly AdditionalChatData _part;

		/// <summary>
		/// Initialises a handle over a part already attached to a host.
		/// </summary>
		/// <param name="host">The chat, message or tool call holding the part.</param>
		/// <param name="part">The part itself.</param>
		public LuaChatPart(IChatAttachmentHost host, AdditionalChatData part)
		{
			_host = host;
			_part = part;
		}

		/// <summary>
		/// Detaches the part from its host, disposing it.
		/// </summary>
		/// <returns><see langword="true"/> when the part was still attached; <see langword="false"/> when it was
		/// replaced, removed earlier, or its host is gone. Never raises.</returns>
		public bool Remove() => _host.RemoveAttachment(_part);

		/// <summary>
		/// Wraps the handle into a <c>chat_part</c> userdata.
		/// </summary>
		/// <param name="part">The handle to wrap.</param>
		[LuaHidden]
		public static LuaUserData Wrap(LuaChatPart part)
			=> new(part, "chat_part")
			{
				Metatable = UserDataMetatableGenerator.GetOrCreate(typeof(LuaChatPart))
			};
	}
}
