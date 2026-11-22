using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace LLMDesktopAssistant.Utils.Noise
{
	/// <summary>
	/// Rasterizes a noise field into an image, optionally normalizing the sampled range and colouring it through a gradient.
	/// </summary>
	public static class NoiseRenderer
	{
		/// <summary>
		/// Samples <paramref name="noise"/> once per pixel, over the given window of noise space.
		/// </summary>
		/// <remarks>
		/// The window defaults to one noise unit per pixel starting at the origin, so the picture size and the field
		/// size are the same until a <paramref name="region"/> says otherwise; a region decouples them, which is how the
		/// caller zooms or stretches the pattern without touching <see cref="NoiseBase.Scale"/>. Samples are taken at
		/// pixel centres. The generator's own scale still applies on top of the window.
		/// With <paramref name="normalize"/> the lowest and the highest sample become the ends of the gradient, which is
		/// what makes narrow-band fractals such as <see cref="FractalType.Ridged"/> usable.
		/// </remarks>
		public static Image<Rgba32> Render(
			NoiseBase noise,
			int width,
			int height,
			NoiseRenderRegion? region = null,
			bool normalize = false,
			IReadOnlyList<Rgba32>? palette = null)
		{
			ArgumentNullException.ThrowIfNull(noise);
			ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
			ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);

			var area = region ?? new NoiseRenderRegion(0.0, 0.0, width, height);
			if (!double.IsFinite(area.X) || !double.IsFinite(area.Y)
				|| !double.IsFinite(area.Width) || !double.IsFinite(area.Height)
				|| area.Width <= 0.0 || area.Height <= 0.0)
			{
				throw new ArgumentOutOfRangeException(nameof(region),
					"The noise region must be finite and its width and height must be positive.");
			}

			var stepX = area.Width / width;
			var stepY = area.Height / height;
			var samples = new float[width * height];
			var min = double.MaxValue;
			var max = double.MinValue;

			for (var y = 0; y < height; y++)
			{
				for (var x = 0; x < width; x++)
				{
					var value = noise.Generate(area.X + ((x + 0.5) * stepX), area.Y + ((y + 0.5) * stepY));
					if (!double.IsFinite(value))
						value = 0.0;

					samples[(y * width) + x] = (float)value;

					if (value < min)
						min = value;
					if (value > max)
						max = value;
				}
			}

			var range = max - min;
			var stretch = normalize && range > double.Epsilon;
			var image = new Image<Rgba32>(width, height);

			image.ProcessPixelRows(accessor =>
			{
				for (var y = 0; y < accessor.Height; y++)
				{
					var row = accessor.GetRowSpan(y);
					for (var x = 0; x < accessor.Width; x++)
					{
						var value = samples[(y * width) + x];
						var position = stretch ? (value - min) / range : (value + 1.0) * 0.5;
						row[x] = SampleGradient(palette, Math.Clamp(position, 0.0, 1.0));
					}
				}
			});

			return image;
		}

		/// <summary>
		/// Maps a position in 0..1 onto the gradient, interpolating between evenly spaced stops.
		/// </summary>
		private static Rgba32 SampleGradient(IReadOnlyList<Rgba32>? palette, double position)
		{
			if (palette is null || palette.Count == 0)
			{
				var gray = ToByte(position);
				return new Rgba32(gray, gray, gray);
			}

			if (palette.Count == 1)
				return palette[0];

			var scaled = position * (palette.Count - 1);
			var index = (int)scaled;
			if (index >= palette.Count - 1)
				return palette[^1];

			var fraction = scaled - index;
			var from = palette[index];
			var to = palette[index + 1];
			return new Rgba32(
				Lerp(from.R, to.R, fraction),
				Lerp(from.G, to.G, fraction),
				Lerp(from.B, to.B, fraction),
				Lerp(from.A, to.A, fraction));
		}

		/// <summary>
		/// Converts a normalized channel value into a byte.
		/// </summary>
		private static byte ToByte(double value) => (byte)Math.Clamp(Math.Round(value * 255.0), 0, 255);

		/// <summary>
		/// Interpolates two colour channels.
		/// </summary>
		private static byte Lerp(byte from, byte to, double fraction)
			=> ToByte(((from / 255.0) * (1.0 - fraction)) + ((to / 255.0) * fraction));
	}
}
