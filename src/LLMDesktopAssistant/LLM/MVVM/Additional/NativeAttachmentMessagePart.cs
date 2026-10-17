using LLMDesktopAssistant.Utils.Attachments;
using RCLargeLanguageModels.Messages.Attachments;

namespace LLMDesktopAssistant.LLM.MVVM.Additional
{
	public class NativeAttachmentMessagePart : AdditionalMessagePart, INativeChatAttachment
	{
		private IAttachment? _nativeAttachment;
		/// <summary>
		/// Gets or sets the native attachment that can be sent directly to a large language model.
		/// </summary>
		public IAttachment? NativeAttachment
		{
			get => _nativeAttachment;
			set => SetProperty(ref _nativeAttachment, value);
		}

		/// <inheritdoc/>
		IAttachment? INativeChatAttachment.Native => NativeAttachment;
	}
}
