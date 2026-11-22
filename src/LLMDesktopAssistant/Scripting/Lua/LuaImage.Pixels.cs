using AsyncLua;
using AsyncLua.Values;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace LLMDesktopAssistant.Scripting.Lua
{
	/// <summary>
	/// Pixel-level drawing of <see cref="LuaImage"/>: direct reads and writes plus the two per-pixel fill loops.
	/// </summary>
	public sealed partial class LuaImage
	{
		/// <summary>
		/// Fills the whole image with one colour, transparent when none is given.
		/// </summary>
		public void Clear(string? color = null)
		{
			var pixels = Pixels;
			var fill = color is null ? new Rgba32(0, 0, 0, 0) : ReadColor(new LuaString(color), "image:clear(color)");

			pixels.ProcessPixelRows(accessor =>
			{
				for (var y = 0; y < accessor.Height; y++)
					accessor.GetRowSpan(y).Fill(fill);
			});

			Revision++;
		}

		public void Clear(byte r, byte g, byte b, byte a = 255)
		{
			var pixels = Pixels;
			var fill = new Rgba32(r, g, b, a);

			pixels.ProcessPixelRows(accessor =>
			{
				for (var y = 0; y < accessor.Height; y++)
					accessor.GetRowSpan(y).Fill(fill);
			});

			Revision++;
		}

		/// <summary>
		/// Writes one pixel.
		/// </summary>
		public void SetPixel(int x, int y, string color)
		{
			var pixels = Pixels;
			ValidatePoint(pixels, x, y, "image:set_pixel(x, y, color)");
			pixels[x, y] = ReadColor(new LuaString(color), "image:set_pixel(x, y, color)");
			Revision++;
		}

		/// <summary>
		/// Writes one pixel.
		/// </summary>
		public void SetPixel(int x, int y, byte r, byte g, byte b, byte a = 255)
		{
			var pixels = Pixels;
			ValidatePoint(pixels, x, y, "image:set_pixel(x, y, r, g, b, [a])");
			pixels[x, y] = new Rgba32(r, g, b, a);
			Revision++;
		}

		/// <summary>
		/// Reads one pixel as "#rrggbbaa".
		/// </summary>
		public LuaTuple GetPixel(int x, int y, string mode = "hex")
		{
			var pixels = Pixels;
			ValidatePoint(pixels, x, y, "image:get_pixel(x, y)");
			var pixel = pixels[x, y];
			if (mode == "tuple")
				return new LuaTuple(new LuaNumber(pixel.R), new LuaNumber(pixel.G), new LuaNumber(pixel.B), new LuaNumber(pixel.A));
			return new LuaTuple(new LuaString(ToHex(pixels[x, y])));
		}

		/// <summary>
		/// Fills a rectangle of pixels from a callback that returns a colour string per coordinate.
		/// </summary>
		/// <remarks>
		/// One Lua call per pixel: the callback sees absolute image coordinates and the pixel's current colour, and
		/// returns a colour string, or <see langword="nil"/> to leave the pixel untouched. It must not mutate this image.
		/// </remarks>
		public void Paint(LuaCallingContext context, LuaFunction function, LuaTable? options = null)
		{
			const string call = "image:paint(fn, options)";
			var pixels = Pixels;
			var region = ReadPixelRegion(options, pixels.Width, pixels.Height, call);
			var mode = options?.Get<string>("mode") ?? "hex";
			var changed = false;

			if (mode == "tuple")
				pixels.ProcessPixelRows(accessor =>
				{
					for (var y = region.Top; y < region.Bottom; y++)
					{
						var row = accessor.GetRowSpan(y);
						for (var x = region.Left; x < region.Right; x++)
						{
							var current = row[x];
							var returned = function.Invoke(context,
								new LuaNumber(x), new LuaNumber(y),
								new LuaNumber(current.R), new LuaNumber(current.G),
								new LuaNumber(current.B), new LuaNumber(current.A));
							if (returned.Count == 0 || returned[0] is LuaNil)
								continue;

							row[x] = ReadChannels(returned, call);
							changed = true;
						}
					}
				});
			else
				pixels.ProcessPixelRows(accessor =>
				{
					for (var y = region.Top; y < region.Bottom; y++)
					{
						var row = accessor.GetRowSpan(y);
						for (var x = region.Left; x < region.Right; x++)
						{
							var returned = function.Invoke(context, new LuaNumber(x), new LuaNumber(y), new LuaString(ToHex(row[x])));
							if (returned.Count == 0 || returned[0] is LuaNil)
								continue;

							row[x] = ReadColor(returned[0], call);
							changed = true;
						}
					}
				});

			if (changed)
				Revision++;
		}

		/// <summary>
		/// Reads <c>options.rect = { x, y, width, height }</c> in pixels, defaulting to the whole image.
		/// </summary>
		private static Rectangle ReadPixelRegion(LuaTable? options, int width, int height, string call)
		{
			if (options?.Get("rect") is not LuaTable rect)
				return new Rectangle(0, 0, width, height);

			var x = ReadOption(rect, "x", 0);
			var y = ReadOption(rect, "y", 0);
			var regionWidth = ReadOption(rect, "width", width);
			var regionHeight = ReadOption(rect, "height", height);

			if (regionWidth <= 0 || regionHeight <= 0 || x < 0 || y < 0
				|| x + regionWidth > width || y + regionHeight > height)
			{
				throw new LuaRuntimeException(
					$"{call}: rect {{ x = {x}, y = {y}, width = {regionWidth}, height = {regionHeight} }} does not fit the {width}x{height} image.");
			}

			return new Rectangle(x, y, regionWidth, regionHeight);
		}

		/// <summary>
		/// Reads the RGBA channels a <c>map</c> callback returned, one byte each, three values meaning an opaque pixel.
		/// </summary>
		private static Rgba32 ReadChannels(LuaTuple returned, string call)
		{
			if (returned.Count < 3
				|| returned[0] is not LuaNumber red
				|| returned[1] is not LuaNumber green
				|| returned[2] is not LuaNumber blue)
			{
				throw new LuaRuntimeException(
					$"{call}: fn must return r, g, b[, a] as numbers in 0..255, or nothing to keep the pixel.");
			}

			var alpha = returned.Count > 3 && returned[3] is LuaNumber alphaValue ? alphaValue : new LuaNumber(255);
			return new Rgba32(ReadChannel(red), ReadChannel(green), ReadChannel(blue), ReadChannel(alpha));
		}

		/// <summary>
		/// Rounds a channel into one byte.
		/// </summary>
		private static byte ReadChannel(LuaNumber value)
			=> (byte)Math.Clamp(Math.Round(value.Value), 0.0, 255.0);

		/// <summary>
		/// Reads an integer option of a rectangle, truncating a fractional value.
		/// </summary>
		private static int ReadOption(LuaTable? table, string key, int fallback)
			=> table?.Get(key) is LuaNumber number ? (int)number.Value : fallback;

		/// <summary>
		/// Reads a floating point option, defaulting to the given fallback.
		/// </summary>
		private static double ReadDouble(LuaTable? table, string key, double fallback)
			=> table?.Get(key) is LuaNumber number ? number.Value : fallback;

		/// <summary>
		/// Rejects a coordinate outside the image instead of silently clamping it.
		/// </summary>
		private static void ValidatePoint(Image<Rgba32> pixels, int x, int y, string call)
		{
			if (x < 0 || y < 0 || x >= pixels.Width || y >= pixels.Height)
				throw new LuaRuntimeException($"{call}: point ({x}, {y}) is outside the {pixels.Width}x{pixels.Height} image.");
		}
	}
}
