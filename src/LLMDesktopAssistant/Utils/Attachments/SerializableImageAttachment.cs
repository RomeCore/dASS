using System.Text.Json.Serialization;
using LiteDB;
using RCLargeLanguageModels.Messages.Attachments;
using SixLabors.ImageSharp;

namespace LLMDesktopAssistant.Utils.Attachments
{
	public class SerializableImageAttachment : IImageAttachment
	{
		/// <summary>
		/// The URL of the image. Can be a base64-data url or a regular URL.
		/// </summary>
		public string Url { get; }

		/// <summary>
		/// Creates a new instance of the <see cref="SerializableImageAttachment"/> class.
		/// </summary>
		/// <param name="url">The URL of the image. Can be a base64-data url or a regular URL.</param>
		/// <exception cref="ArgumentNullException">Thrown if the url parameter is null.</exception>
		[JsonConstructor]
		[BsonCtor]
		public SerializableImageAttachment(string url)
		{
			ArgumentNullException.ThrowIfNull(url);
			Url = url;
		}

		/// <summary>
		/// Creates a new instance of the <see cref="SerializableImageAttachment"/> class.
		/// </summary>
		/// <param name="format">The image format (e.g., "png", "jpg").</param>
		/// <param name="base64">The Base64-encoded image data.</param>
		/// <exception cref="ArgumentNullException">Thrown if the format or base64 parameters are null.</exception>
		public SerializableImageAttachment(string format, string base64)
		{
			ArgumentNullException.ThrowIfNull(format);
			ArgumentNullException.ThrowIfNull(base64);
			Url = "data:image/" + format + ";base64," + base64;
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
			Url = "data:image/png;base64," + Convert.ToBase64String(memstream.ToArray());
		}

		/// <summary>
		/// Creates a new instance of <see cref="SerializableImageAttachment"/> from a file.
		/// </summary>
		/// <param name="path">The path to the image file.</param>
		public static SerializableImageAttachment FromFile(string path)
		{
			var image = Image.Load(path);
			using var memstream = new MemoryStream();
			image.SaveAsPng(memstream);
			return new SerializableImageAttachment("data:image/png;base64," + Convert.ToBase64String(memstream.ToArray()));
		}
	}
}