using AsyncLua.Values;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.LLM.Settings;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using LLMDesktopAssistant.Utils.Attachments;
using RCLargeLanguageModels.Messages.Attachments;

namespace LLMDesktopAssistant.Scripting.Lua
{
	/// <summary>
	/// Represents an image object exposed to Lua as UserData.
	/// Wraps SixLabors.ImageSharp.Image with disposal support.
	/// </summary>
	public sealed class LuaImage : IDisposable, ILuaNativeAttachable
	{
		/// <inheritdoc/>
		public IAttachment GetNativeAttachment() => new SerializableImageAttachment(Format, ToBase64());

		private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(30) };

		private readonly IWorkingDirectoryAccessService _fileAccess;
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

		internal LuaImage(IWorkingDirectoryAccessService fileAccess, Image image, string? format = null)
		{
			_fileAccess = fileAccess ?? throw new ArgumentNullException(nameof(fileAccess));
			_image = image ?? throw new ArgumentNullException(nameof(image));
			Format = format ?? DetectFormat(image.Metadata.DecodedImageFormat) ?? "png";
		}

		#region Static factory methods

		/// <summary>
		/// Loads an image from a file path.
		/// </summary>
		public static LuaImage Load(IWorkingDirectoryAccessService fileAccess, string path)
		{
			var fullPath = fileAccess.AccessPath(path, DirectoryAccessMode.Read);
			var image = Image.Load(fullPath);
			var format = Path.GetExtension(fullPath)?.TrimStart('.').ToLowerInvariant();
			return new LuaImage(fileAccess, image, format);
		}

		/// <summary>
		/// Loads an image from base64-encoded string. Format is auto-detected from header bytes.
		/// </summary>
		public static LuaImage LoadBase64(IWorkingDirectoryAccessService fileAccess, string base64)
		{
			var bytes = Convert.FromBase64String(base64);
			var format = DetectFormatFromBytes(bytes);
			var image = Image.Load(bytes);
			return new LuaImage(fileAccess, image, format);
		}

		/// <summary>
		/// Loads an image from a URL.
		/// </summary>
		public static LuaImage LoadUrl(IWorkingDirectoryAccessService fileAccess, string url)
		{
			var task = Task.Run(async () =>
			{
				var response = await _httpClient.GetAsync(url);
				response.EnsureSuccessStatusCode();
				var stream = await response.Content.ReadAsStreamAsync();
				var image = Image.Load(stream);

				var contentType = response.Content.Headers.ContentType?.MediaType;
				var format = contentType switch
				{
					"image/png" => "png",
					"image/jpeg" or "image/jpg" => "jpg",
					"image/webp" => "webp",
					"image/gif" => "gif",
					"image/bmp" => "bmp",
					_ => DetectFormatFromUrl(url) ?? DetectFormat(image.Metadata.DecodedImageFormat) ?? "png"
				};

				return new LuaImage(fileAccess, image, format);
			});

			return task.GetAwaiter().GetResult();
		}

		/// <summary>
		/// Creates a new image with the specified dimensions and optional background color.
		/// </summary>
		public static LuaImage Create(IWorkingDirectoryAccessService fileAccess, int width, int height, string? color = null)
		{
			Rgba32 bgColor = color != null ? ParseColor(color) : new Rgba32(0, 0, 0, 0);
			var image = new Image<Rgba32>(width, height, bgColor);
			return new LuaImage(fileAccess, image, "png");
		}

		/// <summary>
		/// Creates a LuaImage from raw byte data.
		/// </summary>
		public static LuaImage FromBytes(IWorkingDirectoryAccessService fileAccess, byte[] bytes, string? format = null)
		{
			var image = Image.Load(bytes);
			return new LuaImage(fileAccess, image, format ?? DetectFormat(image.Metadata.DecodedImageFormat) ?? "png");
		}

		/// <summary>
		/// Gets image info (width, height, format) without loading the full image into memory.
		/// </summary>
		public static LuaTable GetInfo(string path)
		{
			using var stream = File.OpenRead(path);
			var info = Image.Identify(stream);
			var format = DetectFormat(info.Metadata.DecodedImageFormat)
				?? Path.GetExtension(path)?.TrimStart('.').ToLowerInvariant()
				?? "unknown";

			var result = new LuaTable();
			result["width"] = new LuaNumber(info.Width);
			result["height"] = new LuaNumber(info.Height);
			result["format"] = new LuaString(format);
			return result;
		}

		/// <summary>
		/// Returns a table of supported image formats.
		/// </summary>
		public static LuaTable GetFormats()
		{
			var result = new LuaTable();
			var formats = new[] { "png", "jpg", "jpeg", "bmp", "gif", "webp" };
			for (int i = 0; i < formats.Length; i++)
				result.Set(i + 1, new LuaString(formats[i]));
			return result;
		}

		#endregion

		#region Instance methods (exposed to Lua)

		/// <summary>
		/// Saves the image to a file.
		/// </summary>
		public void Save(string path, LuaTable? optionsTable = null)
		{
			var fullPath = _fileAccess.AccessPath(path, DirectoryAccessMode.Write);

			var opts = optionsTable != null ? OptionsFromTable(optionsTable) : new ImageSaveOptions();
			EnsureNotDisposed();

			var encoder = GetEncoder(fullPath, opts);
			using var stream = File.Create(fullPath);
			_image.Save(stream, encoder);
		}

		/// <summary>
		/// Converts the image to a base64-encoded string.
		/// </summary>
		public string ToBase64(string? format = null)
		{
			EnsureNotDisposed();
			var targetFormat = format ?? Format;
			var encoder = GetEncoderForFormat(targetFormat, new ImageSaveOptions());
			using var memStream = new MemoryStream();
			_image.Save(memStream, encoder);
			return Convert.ToBase64String(memStream.ToArray());
		}

		/// <summary>
		/// Resizes the image.
		/// </summary>
		public void Resize(int width, int height, LuaTable? optionsTable = null)
		{
			EnsureNotDisposed();

			var mode = ResizeMode.Stretch;
			if (optionsTable != null)
			{
				var modeStr = optionsTable.Get("mode") is LuaString modeStrVal ? modeStrVal.Value : null;
				mode = modeStr?.ToLowerInvariant() switch
				{
					"stretch" => ResizeMode.Stretch,
					"contain" => ResizeMode.BoxPad,
					"cover" => ResizeMode.Crop,
					"pad" => ResizeMode.BoxPad,
					"crop" => ResizeMode.Crop,
					_ => ResizeMode.Stretch
				};
			}

			var resizeOptions = new ResizeOptions
			{
				Size = new Size(width, height),
				Mode = mode,
				Sampler = KnownResamplers.Lanczos3
			};

			_image.Mutate(ctx => ctx.Resize(resizeOptions));
		}

		/// <summary>
		/// Crops the image to the specified rectangle.
		/// </summary>
		public void Crop(int x, int y, int width, int height)
		{
			EnsureNotDisposed();
			_image.Mutate(ctx => ctx.Crop(new Rectangle(x, y, width, height)));
		}

		/// <summary>
		/// Rotates the image by the specified number of degrees.
		/// </summary>
		public void Rotate(float degrees)
		{
			EnsureNotDisposed();
			_image.Mutate(ctx => ctx.Rotate(degrees));
		}

		/// <summary>
		/// Flips the image.
		/// </summary>
		public void Flip(string direction)
		{
			EnsureNotDisposed();
			var flipMode = direction?.ToLowerInvariant() switch
			{
				"horizontal" or "h" => FlipMode.Horizontal,
				"vertical" or "v" => FlipMode.Vertical,
				"both" or "hv" => FlipMode.Horizontal | FlipMode.Vertical,
				_ => throw new ArgumentException($"Invalid flip direction: '{direction}'. Use 'horizontal', 'vertical', or 'both'.")
			};
			_image.Mutate(ctx => ctx.Flip(flipMode));
		}

		/// <summary>
		/// Creates a deep copy of the image.
		/// </summary>
		public LuaImage Clone()
		{
			EnsureNotDisposed();
			return new LuaImage(_fileAccess, _image.Clone(ctx => { }), Format);
		}

		/// <summary>
		/// Gets image information as a Lua table.
		/// </summary>
		public LuaTable GetInfoTable()
		{
			EnsureNotDisposed();
			var result = new LuaTable();
			result["width"] = new LuaNumber(Width);
			result["height"] = new LuaNumber(Height);
			result["format"] = new LuaString(Format);
			return result;
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

		#endregion

		#region Private helpers

		private Image EnsureNotDisposed()
		{
			if (_disposed || _image == null)
				throw new ObjectDisposedException(nameof(LuaImage), "Image has been disposed.");
			return _image;
		}

		private static string? DetectFormat(IImageFormat? format)
		{
			if (format == null) return null;
			var name = format.Name.ToLowerInvariant();
			return name switch
			{
				"png" => "png",
				"jpeg" => "jpg",
				"webp" => "webp",
				"gif" => "gif",
				"bmp" => "bmp",
				_ => name
			};
		}

		private static string? DetectFormatFromUrl(string url)
		{
			var ext = Path.GetExtension(url)?.TrimStart('.').ToLowerInvariant();
			return ext switch
			{
				"png" => "png",
				"jpg" or "jpeg" => "jpg",
				"webp" => "webp",
				"gif" => "gif",
				"bmp" => "bmp",
				_ => null
			};
		}

		private static string? DetectFormatFromBytes(byte[] bytes)
		{
			if (bytes.Length < 4) return null;

			if (bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
				return "png";

			if (bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
				return "jpg";

			if (bytes.Length > 12 && bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46
				&& bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50)
				return "webp";

			if (bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x38)
				return "gif";

			if (bytes[0] == 0x42 && bytes[1] == 0x4D)
				return "bmp";

			return null;
		}

		private static Rgba32 ParseColor(string color)
		{
			if (color.StartsWith("#"))
				return Rgba32.ParseHex(color);
			if (color.StartsWith("0x"))
				return Rgba32.ParseHex(color[2..]);

			if (Rgba32.TryParseHex(color, out var parsed))
				return parsed;

			return new Rgba32(0, 0, 0, 0);
		}

		private class ImageSaveOptions
		{
			public int? Quality { get; set; }
		}

		private static ImageSaveOptions OptionsFromTable(LuaTable table)
		{
			var opts = new ImageSaveOptions();
			var quality = table.Get("quality");
			if (quality is LuaNumber q)
				opts.Quality = (int)q.Value;
			return opts;
		}

		private static IImageEncoder GetEncoder(string path, ImageSaveOptions opts)
		{
			var ext = Path.GetExtension(path)?.TrimStart('.').ToLowerInvariant() ?? "png";
			return GetEncoderForFormat(ext, opts);
		}

		private static IImageEncoder GetEncoderForFormat(string format, ImageSaveOptions opts)
		{
			return format switch
			{
				"png" => new PngEncoder(),
				"jpg" or "jpeg" => new JpegEncoder { Quality = opts.Quality ?? 85 },
				"webp" => new WebpEncoder { Quality = opts.Quality ?? 85 },
				"gif" => new SixLabors.ImageSharp.Formats.Gif.GifEncoder(),
				"bmp" => new SixLabors.ImageSharp.Formats.Bmp.BmpEncoder(),
				_ => throw new ArgumentException($"Unsupported format: '{format}'. Supported: png, jpg, webp, gif, bmp.")
			};
		}

		#endregion
	}
}
