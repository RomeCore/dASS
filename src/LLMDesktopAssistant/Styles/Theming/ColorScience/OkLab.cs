using Avalonia.Media;

namespace LLMDesktopAssistant.Styles.Theming.ColorScience
{
	/// <summary>
	/// A colour in the OKLCh space: perceptual lightness, chroma and hue angle in degrees.
	/// </summary>
	public readonly record struct Oklch(double Lightness, double Chroma, double Hue, double Alpha = 1);

	/// <summary>
	/// Converts between sRGB and OKLab/OKLCh and clamps colours into the sRGB gamut.
	/// </summary>
	/// <remarks>
	/// OKLab is used instead of HSL because equal lightness steps must look equal on every hue: an HSL
	/// step turns yellow white and leaves blue almost unchanged, which is why hand-tuned hover colours
	/// drift apart the moment the accent changes.
	/// </remarks>
	public static class OkLab
	{
		private const double gamutTolerance = 0.0005;

		/// <summary>
		/// Splits an sRGB colour into its OKLCh components.
		/// </summary>
		/// <param name="color">The colour to convert.</param>
		/// <returns>The colour in OKLCh.</returns>
		public static Oklch FromColor(Color color)
		{
			var r = Linearize(color.R / 255.0);
			var g = Linearize(color.G / 255.0);
			var b = Linearize(color.B / 255.0);

			var l = Math.Cbrt(0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * b);
			var m = Math.Cbrt(0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * b);
			var s = Math.Cbrt(0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * b);

			var lightness = 0.2104542553 * l + 0.7936177850 * m - 0.0040720468 * s;
			var a = 1.9779984951 * l - 2.4285922050 * m + 0.4505937099 * s;
			var bb = 0.0259040371 * l + 0.7827717662 * m - 0.8086757660 * s;

			var hue = Math.Atan2(bb, a) * 180.0 / Math.PI;
			if (hue < 0)
				hue += 360;

			return new Oklch(lightness, Math.Sqrt(a * a + bb * bb), hue, color.A / 255.0);
		}

		/// <summary>
		/// Builds an sRGB colour from its OKLCh components, reducing chroma until it fits the gamut.
		/// </summary>
		/// <param name="color">The colour to realise.</param>
		/// <returns>An opaque colour inside the sRGB gamut.</returns>
		public static Color ToColor(Oklch color)
		{
			var chroma = Math.Min(color.Chroma, MaxChromaInGamut(color.Lightness, color.Hue));
			var radians = color.Hue * Math.PI / 180.0;
			var a = chroma * Math.Cos(radians);
			var b = chroma * Math.Sin(radians);

			var l = Math.Pow(color.Lightness + 0.3963377774 * a + 0.2158037573 * b, 3);
			var m = Math.Pow(color.Lightness - 0.1055613458 * a - 0.0638541728 * b, 3);
			var s = Math.Pow(color.Lightness - 0.0894841775 * a - 1.2914855480 * b, 3);

			var r = 4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s;
			var g = -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s;
			var bb = -0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s;

			var alpha = (byte)Math.Clamp(Math.Round(color.Alpha * 255), 0, 255);
			return Color.FromArgb(alpha, Channel(r), Channel(g), Channel(bb));
		}

		/// <summary>
		/// Finds the largest chroma that still fits the sRGB gamut at the given lightness and hue.
		/// </summary>
		/// <param name="lightness">The OKLab lightness.</param>
		/// <param name="hue">The hue angle in degrees.</param>
		/// <returns>The largest chroma that stays inside the gamut.</returns>
		private static double MaxChromaInGamut(double lightness, double hue)
		{
			var low = 0.0;
			var high = 0.5;

			for (var i = 0; i < 24; i++)
			{
				var middle = (low + high) / 2;

				if (InGamut(lightness, middle, hue))
					low = middle;
				else
					high = middle;
			}

			return low;
		}

		/// <summary>
		/// Tests whether an OKLCh colour lands inside the sRGB cube.
		/// </summary>
		private static bool InGamut(double lightness, double chroma, double hue)
		{
			var radians = hue * Math.PI / 180.0;
			var a = chroma * Math.Cos(radians);
			var b = chroma * Math.Sin(radians);

			var l = Math.Pow(lightness + 0.3963377774 * a + 0.2158037573 * b, 3);
			var m = Math.Pow(lightness - 0.1055613458 * a - 0.0638541728 * b, 3);
			var s = Math.Pow(lightness - 0.0894841775 * a - 1.2914855480 * b, 3);

			return IsChannelInGamut(4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s)
				&& IsChannelInGamut(-1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s)
				&& IsChannelInGamut(-0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s);
		}

		private static bool IsChannelInGamut(double channel)
			=> channel >= -gamutTolerance && channel <= 1 + gamutTolerance;

		private static byte Channel(double linear)
			=> (byte)Math.Clamp(Math.Round(Delinearize(Math.Clamp(linear, 0, 1)) * 255), 0, 255);

		private static double Linearize(double channel)
			=> channel <= 0.04045 ? channel / 12.92 : Math.Pow((channel + 0.055) / 1.055, 2.4);

		private static double Delinearize(double channel)
			=> channel <= 0.0031308 ? channel * 12.92 : 1.055 * Math.Pow(channel, 1 / 2.4) - 0.055;
	}
}
