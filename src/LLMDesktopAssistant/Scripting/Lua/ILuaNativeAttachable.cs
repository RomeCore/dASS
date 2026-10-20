using RCLargeLanguageModels.Messages.Attachments;

namespace LLMDesktopAssistant.Scripting.Lua
{
	/// <summary>
	/// A Lua-side handle that can produce a native attachment (an image, eventually a file).
	/// </summary>
	/// <remarks>
	/// This is what <c>chat_message:append_attachment_part()</c> accepts: instead of checking
	/// for a concrete handle type, the Lua API checks for this interface, so a new attachable
	/// kind plugs in without touching the API surface.
	/// </remarks>
	public interface ILuaNativeAttachable
	{
		/// <summary>
		/// Gets the native attachment this handle represents.
		/// </summary>
		/// <returns>The attachment sent to the model.</returns>
		IAttachment GetNativeAttachment();
	}
}
