using LLMDesktopAssistant.LLM.MVVM.Additional;
using LLMDesktopAssistant.Utils.Attachments;

namespace LLMDesktopAssistant.Scripting.Lua
{
	/// <summary>
	/// Converts a Lua-side attachable handle (a <c>uicontrol</c>, a <c>terminal</c>, ...) into the payload
	/// such a handle stands for.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Handlers are what keeps <c>append_data</c> from knowing every concrete Lua type: the Lua API
	/// passes the handle here, the first handler that recognises it builds the payload and returns it.
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
		/// Builds the payload the handle represents.
		/// </summary>
		/// <param name="attachment">The marker-interface payload produced by the Lua side.</param>
		/// <returns>The payload, or <see langword="null"/> when this handler does not recognise the handle.</returns>
		AdditionalChatData? TryResolve(IChatAttachment attachment);
	}
}
