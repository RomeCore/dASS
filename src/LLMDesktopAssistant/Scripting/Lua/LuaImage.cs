using AsyncLua.Values;
using LLMDesktopAssistant.Utils.Attachments;
using RCLargeLanguageModels.Messages.Attachments;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace LLMDesktopAssistant.Scripting.Lua
{
	/// <summary>
	/// Represents an image handle exposed to Lua as userdata.
	/// Wraps SixLabors.ImageSharp.Image with disposal support.
	/// </summary>
	/// <remarks>
	/// Pixels are always <see cref="Rgba32"/>: loading, creating and restoring normalize the image once in the
	/// constructor, so drawing and encoding never branch on the pixel format.
	/// </remarks>
	public sealed partial class LuaImage : IDisposable, ILuaNativeAttachable
	{
		private Image _image;
		private bool _disposed;

		/// <summary>
		/// Gets the width of the image in pixels.
		/// </summary>
		public int Width => EnsureNotDisposed().Width;

		/// <summary>
		/// Gets the height of the image in pixels.
		/// </summary>
		public int Height => EnsureNotDisposed().Height;

		/// <summary>
		/// Gets the image format (e.g., "png", "jpg", "webp", "gif", "bmp").
		/// </summary>
		public string Format { get; private set; }

		/// <summary>
		/// Gets the underlying ImageSharp image.
		/// </summary>
		public Image Image => EnsureNotDisposed();

		/// <summary>
		/// Gets whether the image has been disposed.
		/// </summary>
		public bool Disposed => _disposed;

		/// <summary>
		/// Gets a number that changes whenever the pixels change, so consumers can cache derived data
		/// (an encoded snapshot, a bitmap) and drop it on the next read after a mutation.
		/// </summary>
		[LuaHidden]
		public int Revision { get; private set; }

		/// <summary>
		/// Gets the RGBA32 view every pixel operation works on.
		/// </summary>
		private Image<Rgba32> Pixels => EnsureNotDisposed() as Image<Rgba32>
			?? throw new InvalidOperationException("LuaImage lost its RGBA32 pixel buffer.");

		/// <inheritdoc/>
		public IAttachment GetNativeAttachment() => new SerializableImageAttachment(Format, ToBase64());

		/// <summary>
		/// Wraps the image into the <c>image</c> userdata the Lua side sees.
		/// </summary>
		public static LuaUserData Wrap(LuaImage image)
		{
			return new LuaUserData(image, "image")
			{
				Metatable = UserDataMetatableGenerator.GetOrCreate(typeof(LuaImage))
			};
		}

		/// <summary>
		/// Wraps an already loaded image, normalizing its pixels to <see cref="Rgba32"/>.
		/// </summary>
		/// <remarks>
		/// The image holds no working directory: a path is resolved from the ambient context by the call that needs
		/// one, so any image can be saved from any script.
		/// </remarks>
		public LuaImage(Image image, string? format = null)
		{
			ArgumentNullException.ThrowIfNull(image);

			Format = format ?? DetectFormat(image.Metadata.DecodedImageFormat) ?? "png";

			// Normalize here rather than per operation: every drawing path needs the same pixel layout.
			if (image is Image<Rgba32> rgba)
			{
				_image = rgba;
			}
			else
			{
				_image = image.CloneAs<Rgba32>();
				image.Dispose();
			}
		}

		/// <summary>
		/// Disposes the underlying image resource.
		/// </summary>
		public void Dispose()
		{
			if (!_disposed)
			{
				_disposed = true;
				_image?.Dispose();
				_image = null!;
			}
		}

		/// <summary>
		/// Returns the image, rejecting every operation after <see cref="Dispose"/>.
		/// </summary>
		private Image EnsureNotDisposed()
		{
			if (_disposed || _image == null)
				throw new ObjectDisposedException(nameof(LuaImage), "Image has been disposed.");
			return _image;
		}
	}
}
