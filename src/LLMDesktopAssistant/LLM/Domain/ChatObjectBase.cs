using LLMDesktopAssistant.LLM.MVVM.Additional;
using LLMDesktopAssistant.Utils.Attachments;
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

		/// <summary>
		/// Adds a payload to this object through the single write path: a UI part is stored as is, a
		/// native or content attachment is wrapped into the corresponding invisible part.
		/// </summary>
		/// <param name="attachment">The payload to add.</param>
		/// <exception cref="InvalidOperationException">The payload is not a known chat attachment.</exception>
		public void AddAttachment(IChatAttachment attachment)
		{
			switch (attachment)
			{
				case AdditionalChatData data:
					AdditionalData.Add(data);
					break;

				case INativeChatAttachment native:
					AdditionalData.Add(new NativeAttachmentMessagePart
					{
						NativeAttachment = native.Native,
						IsVisible = false,
						IsRestorable = false
					});
					break;

				case IContentChatAttachment content:
					AdditionalData.Add(new AdditionalMessageContentPart
					{
						Content = content.Content,
						IsVisible = false,
						IsRestorable = false
					});
					break;

				default:
					throw new InvalidOperationException(
						$"Unsupported chat attachment type: {attachment.GetType()}.");
			}
		}

		public IEnumerable<IAttachment> GetNativeAttachments()
		{
			return [.. AdditionalData.OfType<INativeChatAttachment>()
				.Select(a => a.Native).Where(a => a is not null)!];
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (disposing)
			{
				foreach (var viewModel in AdditionalData)
					viewModel.Dispose();
			}
		}
	}
}
