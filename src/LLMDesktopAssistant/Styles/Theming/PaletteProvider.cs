using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Styling;
using LLMDesktopAssistant.Settings.Application;
using LLMDesktopAssistant.Styles.Theming.ColorScience;

namespace LLMDesktopAssistant.Styles.Theming
{
	/// <summary>
	/// Generates the colour rungs every role is mapped onto: neutral, primary and accent ramps plus the
	/// semantic status families, for both theme variants at once.
	/// </summary>
	/// <remarks>
	/// Rungs are an implementation detail: views reference roles from <c>Roles.axaml</c>, never these
	/// keys directly. The provider answers both variants through the <c>theme</c> argument, so a single
	/// merged instance serves light and dark and the variant switch needs no dictionary rebuild.
	/// </remarks>
	public class PaletteProvider : ResourceProvider
	{
		private const double neutralChroma = 0.011;
		private const double rampAnchor = 0.6;

		private static readonly (string Name, double Lightness)[] steps =
		[
			("0", 1.0), ("50", 0.98), ("100", 0.95), ("200", 0.90), ("300", 0.83),
			("400", 0.74), ("500", 0.64), ("600", 0.54), ("700", 0.45), ("800", 0.37),
			("900", 0.29), ("950", 0.22), ("1000", 0.17),
		];

		/// <summary>
		/// The base colours of the semantic families. They carry no theme identity, so they are the same
		/// in every palette; only their derived shades follow the variant.
		/// </summary>
		private static readonly (string Name, Color Color)[] statuses =
		[
			("Success", Color.Parse("#FF2ECC71")),
			("Warning", Color.Parse("#FFD4A017")),
			("Error", Color.Parse("#FFFF4444")),
			("Danger", Color.Parse("#FFFF4444")),
			("Info", Color.Parse("#FF4A9EFF")),
		];

		private Dictionary<string, object> _light = [];
		private Dictionary<string, object> _dark = [];

		/// <summary>
		/// Initializes a new instance of the <see cref="PaletteProvider"/> class from the startup settings.
		/// </summary>
		public PaletteProvider()
		{
			ThemeManager.AttachPalette(this);
			Recompute();
		}

		/// <inheritdoc/>
		public override bool HasResources => _light.Count > 0;

		/// <inheritdoc/>
		public override bool TryGetResource(object key, ThemeVariant? theme, out object? value)
		{
			value = null;

			if (key is not string name)
				return false;

			var resources = theme == ThemeVariant.Light ? _light : _dark;
			return resources.TryGetValue(name, out value);
		}

		/// <summary>
		/// Rebuilds both variants from the current seeds and tells the application the colours changed.
		/// </summary>
		public void Recompute()
		{
			var settings = ApplicationStartupSettingsAccessor.Current;

			_light = Build(settings.Primary, settings.Accent, dark: false);
			_dark = Build(settings.Primary, settings.Accent, dark: true);

			RaiseResourcesChanged();
		}

		/// <summary>
		/// Builds the full rung set of one variant.
		/// </summary>
		private static Dictionary<string, object> Build(Color primary, Color accent, bool dark)
		{
			var resources = new Dictionary<string, object>();
			var primarySeed = OkLab.FromColor(primary);
			var accentSeed = OkLab.FromColor(accent);

			// The neutral ramp is tinted by the primary hue, which is what keeps the surfaces from
			// reading as a flat grey while staying well below the threshold of "coloured".
			AddRamp(resources, "Neutral", primarySeed.Hue, neutralChroma, rampAnchor);
			AddRamp(resources, "Primary", primarySeed.Hue, primarySeed.Chroma, primarySeed.Lightness);
			AddRamp(resources, "Accent", accentSeed.Hue, accentSeed.Chroma, accentSeed.Lightness);

			var baseStep = dark ? "500" : "600";
			Publish(resources, "PrimaryOn", Contrast.PickOn((Color)resources["Primary" + baseStep]));
			Publish(resources, "AccentOn", Contrast.PickOn((Color)resources["Accent" + baseStep]));

			AddStatuses(resources, dark);

			return resources;
		}

		/// <summary>
		/// Expands one seed into the full lightness ladder, tapering chroma away from the seed lightness
		/// so that near-white and near-black steps do not turn garish.
		/// </summary>
		private static void AddRamp(Dictionary<string, object> resources, string prefix, double hue, double chroma, double anchor)
		{
			foreach (var (name, lightness) in steps)
			{
				var taper = Math.Max(0.12, 1 - 0.9 * Math.Min(1, Math.Abs(lightness - anchor) / 0.55));
				Publish(resources, prefix + name, OkLab.ToColor(new Oklch(lightness, chroma * taper, hue)));
			}
		}

		/// <summary>
		/// Publishes the status families: an opaque base, and the overlay, line and on-colour that are
		/// derived from it per variant.
		/// </summary>
		private static void AddStatuses(Dictionary<string, object> resources, bool dark)
		{
			foreach (var (name, baseColor) in statuses)
			{
				var seed = OkLab.FromColor(baseColor);
				var lightness = dark ? Math.Min(0.82, seed.Lightness + 0.06) : seed.Lightness;
				var color = OkLab.ToColor(new Oklch(lightness, seed.Chroma, seed.Hue));
				var key = "Status" + name;

				Publish(resources, key + "500", color);
				Publish(resources, key + "Overlay", WithAlpha(color, dark ? 0.18 : 0.12));
				Publish(resources, key + "Line", WithAlpha(color, dark ? 0.50 : 0.42));
				Publish(resources, key + "On", Contrast.PickOn(color));
			}
		}

		/// <summary>
		/// Replaces the alpha channel of a colour, keeping its red, green and blue channels.
		/// </summary>
		private static Color WithAlpha(Color color, double alpha)
			=> Color.FromArgb((byte)Math.Clamp(Math.Round(alpha * 255), 0, 255), color.R, color.G, color.B);

		/// <summary>
		/// Publishes a colour together with the brush built from it, so templates can use the brush
		/// directly while code and animations read the colour.
		/// </summary>
		private static void Publish(Dictionary<string, object> resources, string name, Color color)
		{
			resources[name] = color;
			resources[name + "Brush"] = new ImmutableSolidColorBrush(color);
		}
	}
}
