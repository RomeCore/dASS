using LLMDesktopAssistant.LLM.MVVM.Additional;
using RCLargeLanguageModels.Messages.Attachments;

namespace LLMDesktopAssistant.LLM.Domain
{
	/// <summary>
	/// Represents the base class for chat objects, such as messages, tool calls and chats.
	/// </summary>
	public class ChatObjectBase : NotifyPropertyChanged
	{
		/// <summary>
		/// Gets or sets the timestamp when the chat object was created.
		/// </summary>
		public DateTime CreatedAt { get; internal set; }

		/// <summary>
		/// The collection of additional view models associated with this chat message.
		/// These can be used for displaying extra information in the UI or store additional data.
		/// </summary>
		public AdditionalChatDataCollection AdditionalData
		{
			get => field ??= [];
			set => (field ??= []).Reset(value);
		}

		public IEnumerable<IAttachment> GetNativeAttachments()
		{
			return [.. AdditionalData.OfType<NativeAttachmentMessagePart>()
				.Select(a => a.NativeAttachment!).Where(a => a is not null)];
		}
	}
}
