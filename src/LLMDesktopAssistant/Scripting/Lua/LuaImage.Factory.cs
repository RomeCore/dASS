using AsyncLua.Values;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;

namespace LLMDesktopAssistant.Scripting.Lua
{
	/// <summary>
	/// The factories of <see cref="LuaImage"/>: loading, creating and format detection.
	/// </summary>
	public sealed partial class LuaImage
	{
		private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(30) };

		/// <summary>
		/// Loads an image from a resolved file path.
		/// </summary>
		public static LuaImage Load(string fullPath)
		{
			var image = Image.Load(fullPath);
			var format = Path.GetExtension(fullPath)?.TrimStart('.').ToLowerInvariant();
			return new LuaImage(image, format);
		}

		/// <summary>
		/// Loads an image from base64-encoded string. Format is auto-detected from header bytes.
		/// </summary>
		public static LuaImage LoadBase64(string base64)
		{
			var bytes = Convert.FromBase64String(base64);
			var format = DetectFormatFromBytes(bytes);
			var image = Image.Load(bytes);
			return new LuaImage(image, format);
		}

		/// <summary>
		/// Loads an image from a URL.
		/// </summary>
		public static async Task<LuaImage> LoadUrlAsync(string url, CancellationToken cancellationToken = default)
		{
			var response = await _httpClient.GetAsync(url, cancellationToken);
			response.EnsureSuccessStatusCode();
			var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
			var image = await Image.LoadAsync(stream, cancellationToken);

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

			return new LuaImage(image, format);
		}

		/// <summary>
		/// Creates a new image with the specified dimensions and optional background color.
		/// </summary>
		public static LuaImage Create(int width, int height, string? color = null)
		{
			Rgba32 bgColor = color != null ? ParseColor(color) : new Rgba32(0, 0, 0, 0);
			var image = new Image<Rgba32>(width, height, bgColor);
			return new LuaImage(image, "png");
		}

		/// <summary>
		/// Restores an image from encoded bytes without binding it to a working directory (a persisted value).
		/// </summary>
		internal static LuaImage FromSnapshot(byte[] bytes, string? format)
		{
			var image = Image.Load(bytes);
			return new LuaImage(image, format ?? DetectFormat(image.Metadata.DecodedImageFormat) ?? "png");
		}

		/// <summary>
		/// Creates a LuaImage from raw byte data.
		/// </summary>
		public static LuaImage FromBytes(byte[] bytes, string? format = null)
		{
			var image = Image.Load(bytes);
			return new LuaImage(image, format ?? DetectFormat(image.Metadata.DecodedImageFormat) ?? "png");
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

		/// <summary>
		/// Maps an ImageSharp format onto the short name the API speaks.
		/// </summary>
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

		/// <summary>
		/// Guesses the format from the URL extension.
		/// </summary>
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

		/// <summary>
		/// Guesses the format from the header bytes of an encoded image.
		/// </summary>
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
	}
}
