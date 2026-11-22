using AsyncLua;
using AsyncLua.Values;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace LLMDesktopAssistant.Scripting.Lua
{
	/// <summary>
	/// Filters of <see cref="LuaImage"/>: the ImageSharp processing passes a script reaches most often.
	/// </summary>
	public sealed partial class LuaImage
	{
		/// <summary>
		/// Inverts the colour channels, and the alpha channel too when <paramref name="alpha"/> is set.
		/// </summary>
		/// <remarks>
		/// Written out pixel by pixel instead of going through the ImageSharp filter, whose treatment of alpha is an
		/// implementation detail this API must not inherit.
		/// </remarks>
		public void Invert(bool alpha = false)
		{
			var pixels = Pixels;
			pixels.ProcessPixelRows(accessor =>
			{
				for (var y = 0; y < accessor.Height; y++)
				{
					var row = accessor.GetRowSpan(y);
					for (var x = 0; x < row.Length; x++)
					{
						var pixel = row[x];
						row[x] = new Rgba32(
							(byte)(255 - pixel.R),
							(byte)(255 - pixel.G),
							(byte)(255 - pixel.B),
							alpha ? (byte)(255 - pixel.A) : pixel.A);
					}
				}
			});

			Revision++;
		}

		/// <summary>
		/// Drops the colour, keeping luminance.
		/// </summary>
		public void Grayscale() => Apply(context => context.Grayscale());

		/// <summary>
		/// Applies the sepia tone.
		/// </summary>
		public void Sepia() => Apply(context => context.Sepia());

		/// <summary>
		/// Multiplies the brightness: one leaves the image unchanged, zero is black.
		/// </summary>
		public void Brightness(float amount) => Apply(context => context.Brightness(Math.Max(0f, amount)));

		/// <summary>
		/// Pushes the contrast away from mid grey: one leaves the image unchanged, zero is flat grey.
		/// </summary>
		public void Contrast(float amount) => Apply(context => context.Contrast(Math.Max(0f, amount)));

		/// <summary>
		/// Scales the alpha channel, 0..1.
		/// </summary>
		public void Opacity(float amount) => Apply(context => context.Opacity(Math.Clamp(amount, 0f, 1f)));

		/// <summary>
		/// Applies a Gaussian blur of the given sigma.
		/// </summary>
		public void Blur(float sigma)
		{
			if (sigma <= 0f)
				throw new LuaRuntimeException("image:blur(sigma): sigma must be positive.");

			Apply(context => context.GaussianBlur(sigma));
		}

		/// <summary>
		/// Turns the image black and white, cutting at a threshold in 0..1.
		/// </summary>
		public void Threshold(float value) => Apply(context => context.BinaryThreshold(Math.Clamp(value, 0f, 1f)));

		/// <summary>
		/// Averages blocks of <paramref name="size"/> by <paramref name="size"/> pixels.
		/// </summary>
		public void Pixelate(int size)
		{
			if (size <= 0)
				throw new LuaRuntimeException("image:pixelate(size): size must be positive.");

			Apply(context => context.Pixelate(size));
		}

		/// <summary>
		/// Runs one processing pass and invalidates everything derived from the pixels.
		/// </summary>
		private void Apply(Action<IImageProcessingContext> operation)
		{
			EnsureNotDisposed();
			_image.Mutate(operation);
			Revision++;
		}
	}
}
