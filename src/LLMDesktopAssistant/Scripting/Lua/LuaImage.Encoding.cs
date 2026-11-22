using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.LLM.Services;
using Microsoft.Extensions.DependencyInjection;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;

namespace LLMDesktopAssistant.Scripting.Lua
{
	/// <summary>
	/// Saving and encoding of <see cref="LuaImage"/>: the file and byte forms the API hands out.
	/// </summary>
	public sealed partial class LuaImage
	{
		/// <summary>
		/// Saves the image to a file, resolving a relative path against the working directory of the ambient context.
		/// </summary>
		public void Save(LuaCallingContext context, string path, LuaTable? optionsTable = null)
		{
			var fullPath = context.GetAmbient().Services
				.GetRequiredService<IWorkingDirectoryAccessService>()
				.GetFullPath(path);

			var opts = optionsTable != null ? OptionsFromTable(optionsTable) : new ImageSaveOptions();
			EnsureNotDisposed();

			var encoder = GetEncoder(fullPath, opts);
			using var stream = File.Create(fullPath);
			_image.Save(stream, encoder);
		}

		/// <summary>
		/// Converts the image to a base64-encoded string.
		/// </summary>
		public string ToBase64(string? format = null) => Convert.ToBase64String(ToBytes(format));

		/// <summary>
		/// Encodes the image into bytes in the given format (the original one when omitted).
		/// </summary>
		public byte[] ToBytes(string? format = null)
		{
			EnsureNotDisposed();
			var encoder = GetEncoderForFormat(format ?? Format, new ImageSaveOptions());
			using var memStream = new MemoryStream();
			_image.Save(memStream, encoder);
			return memStream.ToArray();
		}

		/// <summary>
		/// Reads the encoding options of a save call; only <c>quality</c> is honoured.
		/// </summary>
		private static ImageSaveOptions OptionsFromTable(LuaTable table)
		{
			var opts = new ImageSaveOptions();
			var quality = table.Get("quality");
			if (quality is LuaNumber q)
				opts.Quality = (int)q.Value;
			return opts;
		}

		/// <summary>
		/// Picks the encoder from the target path's extension.
		/// </summary>
		private static IImageEncoder GetEncoder(string path, ImageSaveOptions opts)
		{
			var ext = Path.GetExtension(path)?.TrimStart('.').ToLowerInvariant() ?? "png";
			return GetEncoderForFormat(ext, opts);
		}

		/// <summary>
		/// Picks the encoder for a format name.
		/// </summary>
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

		/// <summary>
		/// The encoding options of a save call.
		/// </summary>
		private sealed class ImageSaveOptions
		{
			/// <summary>
			/// Gets or sets the lossy quality of JPEG and WebP output.
			/// </summary>
			public int? Quality { get; set; }
		}
	}
}
