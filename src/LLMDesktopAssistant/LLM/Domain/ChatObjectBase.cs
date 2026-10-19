using System.Net.Mail;
using System.Runtime.CompilerServices;
using DocumentFormat.OpenXml.Wordprocessing;
using LLMDesktopAssistant.Agents.Tasks;
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

		private readonly ConditionalWeakTable<IChatAttachment, AdditionalChatData> _attachmentMap = [];

		public void AddAttachment(IChatAttachment attachment)
		{
			if (attachment is AdditionalChatData data)
			{
				AdditionalData.Add(data);
				return;
			}

			if (_attachmentMap.TryGetValue(attachment, out var existing))
			{
				AdditionalData.Add(existing);
				return;
			}

			switch (attachment)
			{
				case INativeChatAttachment native:
					AdditionalChatData? converted = new NativeAttachmentMessagePart
					{
						NativeAttachment = native.Native,
						IsVisible = false,
						IsRestorable = false
					};
					_attachmentMap.Add(attachment, converted);
					AdditionalData.Add(converted);
					break;

				case IContentChatAttachment content:
					converted = new AdditionalMessageContentPart
					{
						Content = content.Content,
						IsVisible = false,
						IsRestorable = false
					};
					_attachmentMap.Add(attachment, converted);
					AdditionalData.Add(converted);
					break;
			}
		}

		public bool RemoveAttachment(IChatAttachment attachment)
		{
			if (attachment is AdditionalChatData data)
				return AdditionalData.Remove(data);

			if (_attachmentMap.TryGetValue(attachment, out var existing))
				return AdditionalData.Remove(existing);

			return false;
		}

		public void ClearAttachments()
		{
			AdditionalData.Clear();
		}

		public IEnumerable<IChatAttachment> GetAttachments()
		{
			return AdditionalData;
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
