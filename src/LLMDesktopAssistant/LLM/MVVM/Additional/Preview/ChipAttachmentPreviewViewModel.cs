using Avalonia.Media.Imaging;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.MVVM;
using RCLargeLanguageModels.Messages.Attachments;

namespace LLMDesktopAssistant.LLM.MVVM.Additional.Preview
{
	/// <summary>
	/// The preview of an attachment part: the picture the chip hides, when there is one, and a one-line caption
	/// saying what kind of attachment it is.
	/// </summary>
	/// <remarks>
	/// The picture is decoded on first read rather than in the constructor, because the preview object is created
	/// with the chip while the tooltip is only realized on hover. It is never fetched: a remote URL stays a caption,
	/// since a tooltip must not start a network request.
	/// </remarks>
	[ViewModelFor(typeof(ChipAttachmentPreviewView))]
	public sealed class ChipAttachmentPreviewViewModel : ViewModelBase
	{
		private readonly IAttachment? _attachment;

		/// <summary>
		/// Initialises the preview of an attachment part.
		/// </summary>
		/// <param name="attachment">The native attachment the part carries, or <see langword="null"/> for a part
		/// that only knows its source.</param>
		/// <param name="source">The path or URL of the attached file, or <see langword="null"/>.</param>
		public ChipAttachmentPreviewViewModel(IAttachment? attachment, string? source)
		{
			_attachment = attachment;
			Source = string.IsNullOrWhiteSpace(source) ? null : source;
			Caption = attachment switch
			{
				IImageAttachment => Locale.GetKey("message.chip.media.image"),
				IAudioAttachment => Locale.GetKey("message.chip.media.audio"),
				IVideoAttachment => Locale.GetKey("message.chip.media.video"),
				_ => null
			};
		}

		/// <summary>
		/// Gets the decoded picture, or <see langword="null"/> when the attachment is not an image or could not be
		/// read.
		/// </summary>
		public Bitmap? Image => field ??= Decode(_attachment);

		/// <summary>
		/// Gets a value indicating whether there is a picture to show.
		/// </summary>
		public bool HasImage => Image is not null;

		/// <summary>
		/// Gets the localized name of the attachment kind, or <see langword="null"/>.
		/// </summary>
		public LocaleKeyBase? Caption { get; }

		/// <summary>
		/// Gets the path or URL of the attached file, or <see langword="null"/>.
		/// </summary>
		public string? Source { get; }

		private static Bitmap? Decode(IAttachment? attachment)
		{
			if (attachment is not IImageAttachment image)
				return null;

			try
			{
				if (image.Url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
				{
					var comma = image.Url.IndexOf(',');
					return comma < 0
						? null
						: new Bitmap(new MemoryStream(Convert.FromBase64String(image.Url[(comma + 1)..])));
				}

				return File.Exists(image.Url) ? new Bitmap(image.Url) : null;
			}
			catch
			{
				// A preview that cannot be produced is not an error: the caption still describes the attachment.
				return null;
			}
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (disposing)
				Image?.Dispose();
		}
	}
}
