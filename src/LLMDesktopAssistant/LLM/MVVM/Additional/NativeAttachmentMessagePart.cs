using LiteDB;
using LLMDesktopAssistant.LLM.MVVM.Additional.Preview;
using LLMDesktopAssistant.Utils;
using LLMDesktopAssistant.Utils.Attachments;
using RCLargeLanguageModels.Messages.Attachments;

namespace LLMDesktopAssistant.LLM.MVVM.Additional
{
	/// <summary>
	/// A message part that carries an attachment the model can receive, rendered as a chip and previewed as a
	/// picture when the attachment is an image.
	/// </summary>
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

		private ChipAttachmentPreviewViewModel? _preview;
		private IAttachment? _previewOf;

		/// <summary>
		/// Gets the path or URL shown under the picture, or <see langword="null"/> when the part only knows the
		/// native attachment.
		/// </summary>
		protected virtual string? PreviewSource => null;

		/// <inheritdoc/>
		[BsonIgnore]
		[ChangeTracker.Untracked]
		public override object? Preview
		{
			get
			{
				if (NativeAttachment is not { } attachment)
					return null;

				// The attachment is replaced in place when the same key carries a new one, so the preview follows it.
				if (!ReferenceEquals(_previewOf, attachment))
				{
					_preview?.Dispose();
					_preview = new ChipAttachmentPreviewViewModel(attachment, PreviewSource);
					_previewOf = attachment;
				}

				return _preview;
			}
		}

		/// <summary>
		/// Copies the part without the preview: the copy decodes its own picture rather than sharing one bitmap.
		/// </summary>
		public override AdditionalMessagePart Clone()
		{
			var clone = (NativeAttachmentMessagePart)base.Clone();
			clone._preview = null;
			clone._previewOf = null;
			return clone;
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (disposing)
				_preview?.Dispose();
		}
	}
}
