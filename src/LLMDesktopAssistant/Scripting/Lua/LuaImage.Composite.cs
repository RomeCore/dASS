using AsyncLua;
using AsyncLua.Values;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace LLMDesktopAssistant.Scripting.Lua
{
	/// <summary>
	/// Compositing of <see cref="LuaImage"/>: one image pasted over another with a blend mode and opacity.
	/// </summary>
	public sealed partial class LuaImage
	{
		/// <summary>
		/// Draws <paramref name="other"/> over this image, optionally scaling it into a rectangle of this image.
		/// </summary>
		/// <remarks>
		/// Reads <c>x</c>, <c>y</c>, <c>width</c>, <c>height</c>, <c>opacity</c> and <c>mode</c> from the options table;
		/// the pasted rectangle defaults to the size of <paramref name="other"/> at the origin and is clipped to this
		/// image, so an overhanging overlay does not throw.
		/// </remarks>
		public void Composite(LuaImage other, LuaTable? options = null)
		{
			const string call = "image:composite(other, options)";
			if (ReferenceEquals(other, this))
				throw new LuaRuntimeException($"{call}: an image cannot be composited onto itself.");

			other.EnsureNotDisposed();
			var pixels = Pixels;

			var width = ReadOption(options, "width", other.Width);
			var height = ReadOption(options, "height", other.Height);
			if (width <= 0 || height <= 0)
				throw new LuaRuntimeException($"{call}: width and height must be positive.");

			var x = ReadOption(options, "x", 0);
			var y = ReadOption(options, "y", 0);
			var opacity = Math.Clamp(ReadDouble(options, "opacity", 1.0), 0.0, 1.0);
			var mode = ParseCompositeMode(options?.Get<string>("mode"));

			// Scaling is the only case that needs a copy: same-size overlays blend straight from the source.
			var scaled = width != other.Width || height != other.Height;
			var source = scaled ? other.Pixels.Clone(ctx => ctx.Resize(width, height)) : other.Pixels;
			try
			{
				if (Paste(pixels, source, x, y, opacity, mode))
					Revision++;
			}
			finally
			{
				if (scaled)
					source.Dispose();
			}
		}

		/// <summary>
		/// The blend modes <c>image:composite</c> understands.
		/// </summary>
		private enum CompositeMode
		{
			Normal,
			Add,
			Subtract,
			Multiply,
			Screen,
			Overlay,
			Darken,
			Lighten,
			Difference
		}

		/// <summary>
		/// Blends <paramref name="source"/> into <paramref name="target"/> at the given position, clipping to the edges.
		/// </summary>
		private static bool Paste(Image<Rgba32> target, Image<Rgba32> source, int x, int y, double opacity, CompositeMode mode)
		{
			var left = Math.Max(0, x);
			var top = Math.Max(0, y);
			var right = Math.Min(target.Width, x + source.Width);
			var bottom = Math.Min(target.Height, y + source.Height);
			if (right <= left || bottom <= top)
				return false;

			target.ProcessPixelRows(source, (targetRows, sourceRows) =>
			{
				for (var row = top; row < bottom; row++)
				{
					var targetRow = targetRows.GetRowSpan(row);
					var sourceRow = sourceRows.GetRowSpan(row - y);
					for (var column = left; column < right; column++)
						targetRow[column] = Blend(targetRow[column], sourceRow[column - x], mode, opacity);
				}
			});

			return true;
		}

		/// <summary>
		/// Composites one pixel over another: the mode decides the colour, the source alpha and the opacity decide the mix.
		/// </summary>
		private static Rgba32 Blend(Rgba32 below, Rgba32 above, CompositeMode mode, double opacity)
		{
			var alpha = (above.A / 255.0) * opacity;
			if (alpha <= 0.0)
				return below;

			var belowAlpha = below.A / 255.0;
			var alphaOut = alpha + (belowAlpha * (1.0 - alpha));
			if (alphaOut <= 0.0)
				return default;

			return new Rgba32(
				Mix(BlendChannel(mode, below.R, above.R), alpha, below.R, belowAlpha, alphaOut),
				Mix(BlendChannel(mode, below.G, above.G), alpha, below.G, belowAlpha, alphaOut),
				Mix(BlendChannel(mode, below.B, above.B), alpha, below.B, belowAlpha, alphaOut),
				ToByte(alphaOut));
		}

		/// <summary>
		/// Source-over of one blended channel onto the channel below it.
		/// </summary>
		private static byte Mix(byte blended, double alpha, byte below, double belowAlpha, double alphaOut)
			=> ToByte(((blended / 255.0 * alpha) + (below / 255.0 * belowAlpha * (1.0 - alpha))) / alphaOut);

		/// <summary>
		/// Blends one channel pair with the mode, ignoring alpha.
		/// </summary>
		private static byte BlendChannel(CompositeMode mode, byte below, byte above)
		{
			return mode switch
			{
				CompositeMode.Add => (byte)Math.Min(255, below + above),
				CompositeMode.Subtract => (byte)Math.Max(0, below - above),
				CompositeMode.Multiply => (byte)(below * above / 255),
				CompositeMode.Screen => (byte)(255 - ((255 - below) * (255 - above) / 255)),
				CompositeMode.Overlay => below < 128
					? (byte)(2 * below * above / 255)
					: (byte)(255 - (2 * (255 - below) * (255 - above) / 255)),
				CompositeMode.Darken => Math.Min(below, above),
				CompositeMode.Lighten => Math.Max(below, above),
				CompositeMode.Difference => (byte)Math.Abs(below - above),
				_ => above
			};
		}

		/// <summary>
		/// Maps a Lua-facing blend mode name onto the enum, falling back to <see cref="CompositeMode.Normal"/>.
		/// </summary>
		private static CompositeMode ParseCompositeMode(string? name)
		{
			return name?.Trim().ToLowerInvariant() switch
			{
				"add" => CompositeMode.Add,
				"subtract" or "sub" => CompositeMode.Subtract,
				"multiply" or "mul" => CompositeMode.Multiply,
				"screen" => CompositeMode.Screen,
				"overlay" => CompositeMode.Overlay,
				"darken" => CompositeMode.Darken,
				"lighten" => CompositeMode.Lighten,
				"difference" => CompositeMode.Difference,
				_ => CompositeMode.Normal
			};
		}
	}
}
