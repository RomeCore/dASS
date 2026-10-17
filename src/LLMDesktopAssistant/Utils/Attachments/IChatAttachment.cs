using RCLargeLanguageModels.Messages.Attachments;

namespace LLMDesktopAssistant.Utils.Attachments
{
	/// <summary>
	/// A payload that can be added to a chat object (a message or a tool call): a UI part, a native
	/// attachment or a text contribution.
	/// </summary>
	/// <remarks>
	/// The two branches below are what the model-facing consumers filter by. A UI payload
	/// (<see cref="LLMDesktopAssistant.LLM.MVVM.Additional.AdditionalChatData"/>) implements neither and is
	/// therefore invisible to them by construction.
	/// </remarks>
	public interface IChatAttachment;

	/// <summary>
	/// A payload the model receives as a native attachment.
	/// </summary>
	public interface INativeChatAttachment : IChatAttachment
	{
		/// <summary>
		/// The native attachment sent to the model, or <see langword="null"/> when it cannot be produced.
		/// </summary>
		IAttachment? Native { get; }
	}

	/// <summary>
	/// A payload the model receives as text appended to the content.
	/// </summary>
	public interface IContentChatAttachment : IChatAttachment
	{
		/// <summary>
		/// The text contribution.
		/// </summary>
		string Content { get; }
	}
}
