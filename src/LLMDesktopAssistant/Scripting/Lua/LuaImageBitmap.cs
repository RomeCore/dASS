using Avalonia.Media.Imaging;
using LLMDesktopAssistant.Scripting.Lua.Converters;
using Serilog;

namespace LLMDesktopAssistant.Scripting.Lua
{
	/// <summary>
	/// Creates the Avalonia bitmap a dynamic view model binds an image member to.
	/// </summary>
	/// <remarks>
	/// Decoding failures never propagate: a broken image renders as an absent source, the same silent way
	/// a missed binding does, and the reason is logged instead.
	/// </remarks>
	public static class LuaImageBitmap
	{
		/// <summary>
		/// Creates a bitmap from a live Lua image.
		/// </summary>
		/// <param name="image">The image to draw.</param>
		/// <returns>The bitmap, or <see langword="null"/> when the image cannot be decoded.</returns>
		public static Bitmap? FromImage(LuaImage image)
		{
			byte[] bytes;
			try
			{
				bytes = image.ToBytes();
			}
			catch (Exception ex)
			{
				Log.Warning(ex, "Failed to encode a Lua image for display.");
				return null;
			}

			return FromBytes(bytes);
		}

		/// <summary>
		/// Creates a bitmap from a serialized image marker.
		/// </summary>
		/// <param name="marker">The marker string produced by <see cref="LuaImageNodeConverter.DumpImage"/>.</param>
		/// <returns>The bitmap, or <see langword="null"/> when the marker or its payload is malformed.</returns>
		public static Bitmap? FromMarker(string marker)
		{
			return LuaImageNodeConverter.TryParseMarker(marker, out _, out var bytes)
				? FromBytes(bytes)
				: null;
		}

		/// <summary>
		/// Creates a bitmap from encoded image bytes.
		/// </summary>
		/// <param name="bytes">The encoded image.</param>
		/// <returns>The bitmap, or <see langword="null"/> when the bytes are not a supported image.</returns>
		public static Bitmap? FromBytes(byte[] bytes)
		{
			try
			{
				using var stream = new MemoryStream(bytes);
				return new Bitmap(stream);
			}
			catch (Exception ex)
			{
				Log.Warning(ex, "Failed to decode an image for display.");
				return null;
			}
		}
	}
}
