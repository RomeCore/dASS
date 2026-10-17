using System.Text.Json.Serialization;
using LiteDB;
using RCLargeLanguageModels.Messages.Attachments;
using SixLabors.ImageSharp;

namespace LLMDesktopAssistant.Utils.Attachments
{
	public class SerializableImageAttachment : IImageBase64Attachment
	{
		public string Format { get; }
		public string Base64 { get; }

		[JsonIgnore]
		[BsonIgnore]
		public string Url => "data:image/" + Format + ";base64," + Base64;

		/// <summary>
		/// Creates a new instance of the <see cref="SerializableImageAttachment"/> class.
		/// </summary>
		/// <param name="format">The image format (e.g., "png", "jpg").</param>
		/// <param name="base64">The Base64-encoded image data.</param>
		/// <exception cref="ArgumentNullException">Thrown if the format or base64 parameters are null.</exception>
		[JsonConstructor]
		[BsonCtor]
		public SerializableImageAttachment(string format, string base64)
		{
			Format = format ?? throw new ArgumentNullException(nameof(format));
			Base64 = base64 ?? throw new ArgumentNullException(nameof(base64));
		}

		/// <summary>
		/// Creates a new instance of <see cref="SerializableImageAttachment"/> from an ImageSharp <see cref="Image"/>.
		/// Converts image to PNG format and encodes it as Base64.
		/// </summary>
		/// <param name="image">The ImageSharp <see cref="Image"/> to create the attachment from.</param>
		public SerializableImageAttachment(Image image)
		{
			using var memstream = new MemoryStream();
			image.SaveAsPng(memstream);
			Format = "png";
			Base64 = Convert.ToBase64String(memstream.ToArray());
		}

		/// <summary>
		/// Creates a new instance of <see cref="SerializableImageAttachment"/> from an image file path.
		/// </summary>
		/// <param name="path">The file path of the image to create the attachment from.</param>
		public SerializableImageAttachment(string path)
		{
			var image = Image.Load(path);
			using var memstream = new MemoryStream();
			image.SaveAsPng(memstream);
			Format = "png";
			Base64 = Convert.ToBase64String(memstream.ToArray());
		}
	}
}