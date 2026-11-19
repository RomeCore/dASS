using Avalonia.Controls;
using Avalonia.Styling;
using LLMDesktopAssistant.Settings.Application;

namespace LLMDesktopAssistant.Styles.Theming
{
	/// <summary>
	/// Generates the type scale from a base size and a ratio, including the line heights that go with it.
	/// </summary>
	/// <remarks>
	/// Sizes are rounded to half a pixel so that a scale step never lands on a value the renderer has to
	/// re-round per glyph. The profile is a per-platform default the user can override, not a breakpoint.
	/// </remarks>
	public class TypographyProvider : ResourceProvider
	{
		private const double lineHeightFactor = 1.45;

		private static readonly (string Name, int Step)[] steps =
		[
			("Xs", -2), ("S", -1), ("M", 0), ("L", 1), ("Xl", 2), ("2Xl", 3), ("3Xl", 4),
		];

		private readonly Dictionary<string, object> _resources = [];

		/// <summary>
		/// Initializes a new instance of the <see cref="TypographyProvider"/> class from the startup settings.
		/// </summary>
		public TypographyProvider()
		{
			ThemeManager.AttachTypography(this);
			Recompute();
		}

		/// <inheritdoc/>
		public override bool HasResources => _resources.Count > 0;

		/// <inheritdoc/>
		public override bool TryGetResource(object key, ThemeVariant? theme, out object? value)
		{
			value = null;
			return key is string name && _resources.TryGetValue(name, out value);
		}

		/// <summary>
		/// Rebuilds the scale from the current profile and accessibility multiplier.
		/// </summary>
		public void Recompute()
		{
			var settings = ApplicationStartupSettingsAccessor.Current;
			var (baseSize, ratio) = Profile(settings.FontProfile);
			var scale = settings.FontScale <= 0 ? 1 : settings.FontScale;

			foreach (var (name, step) in steps)
			{
				var size = Round(baseSize * Math.Pow(ratio, step) * scale);
				_resources["FontSize" + name] = size;
				_resources["LineHeight" + name] = Math.Round(size * lineHeightFactor);
			}

			RaiseResourcesChanged();
		}

		/// <summary>
		/// Maps a profile onto its base size and step ratio.
		/// </summary>
		private static (double BaseSize, double Ratio) Profile(FontProfile profile) => profile switch
		{
			FontProfile.Compact => (13, 1.18),
			FontProfile.Large => (16, 1.22),
			_ => (14, 1.20),
		};

		private static double Round(double value)
			=> Math.Round(value * 2, MidpointRounding.AwayFromZero) / 2;
	}
}
