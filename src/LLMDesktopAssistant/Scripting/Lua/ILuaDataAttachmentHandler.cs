using LLMDesktopAssistant.Utils.Attachments;

namespace LLMDesktopAssistant.Scripting.Lua
{
	/// <summary>
	/// Converts a Lua-side attachable handle (a <c>uicontrol</c>, a <c>terminal</c>, ...) into a payload
	/// and attaches it to the host the script asked for.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Handlers are what keeps <c>append_data</c> from knowing every concrete Lua type: the Lua API
	/// passes the handle here, the first handler that recognises it attaches it and returns
	/// <see langword="true"/>.
	/// </para>
	/// <para>
	/// <see cref="IChatAttachment"/> is a marker interface, and it is the whole contract: a Lua handle
	/// that can be attached implements it, which is the proof the script cannot forge.
	/// </para>
	/// </remarks>
	public interface ILuaDataAttachmentHandler
	{
		/// <summary>
		/// The capture priority. Lower values are asked first. Handlers that recognise disjoint handle
		/// types should keep the same value; this is not an insert-order knob.
		/// </summary>
		int Order { get; }

		/// <summary>
		/// Attaches the payload the handle represents to the host.
		/// </summary>
		/// <param name="host">The host the payload is attached to.</param>
		/// <param name="attachment">The marker-interface payload produced by the Lua side.</param>
		/// <returns><see langword="true"/> when this handler recognised the payload and attached it.</returns>
		bool TryAttach(IChatAttachmentHost host, IChatAttachment attachment);
	}
}
