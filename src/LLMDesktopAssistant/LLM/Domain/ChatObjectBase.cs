using System.Runtime.CompilerServices;
using LLMDesktopAssistant.LLM.MVVM.Additional;
using LLMDesktopAssistant.Utils.Attachments;
using RCLargeLanguageModels.Messages.Attachments;

namespace LLMDesktopAssistant.LLM.Domain
{
	/// <summary>
	/// Represents the base class for chat objects, such as messages, tool calls and chats.
	/// </summary>
	public class ChatObjectBase : NotifyPropertyChanged, IChatAttachmentHost
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
			AdditionalChatData? removedPart = null;
			bool removed;

			if (attachment is AdditionalChatData data)
			{
				removed = AdditionalData.Remove(data);
				removedPart = data;
			}
			else if (_attachmentMap.TryGetValue(attachment, out var existing))
			{
				removed = AdditionalData.Remove(existing);
				removedPart = existing;
				if (removed)
					_attachmentMap.Remove(attachment);
			}
			else
			{
				removed = false;
			}

			// A removed payload may own a live resource (a terminal session owns a process),
			// so detaching it is its only death signal.
			if (removed)
				removedPart?.Dispose();

			return removed;
		}

		public void ClearAttachments()
		{
			var parts = AdditionalData.ToArray();
			AdditionalData.Clear();

			foreach (var part in parts)
				part.Dispose();
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
