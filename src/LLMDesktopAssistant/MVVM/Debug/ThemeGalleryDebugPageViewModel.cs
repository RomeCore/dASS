using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using LLMDesktopAssistant.Settings.Application;
using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.MVVM.Debug
{
	/// <summary>
	/// A read-only snapshot of one colour rung: its resource key, the colour and a brush to paint it.
	/// </summary>
	public sealed class ThemeSwatch(string name, Color color)
	{
		/// <summary>
		/// Gets the resource key of the rung.
		/// </summary>
		public string Name { get; } = name;

		/// <summary>
		/// Gets the colour of the rung.
		/// </summary>
		public Color Color { get; } = color;

		/// <summary>
		/// Gets the brush built from <see cref="Color"/>.
		/// </summary>
		public IBrush Brush { get; } = new SolidColorBrush(color);

		/// <summary>
		/// Gets the colour formatted as <c>#RRGGBB</c>.
		/// </summary>
		public string Hex { get; } = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
	}

	/// <summary>
	/// One step of the type scale: its key, the size, the line height and a sample string.
	/// </summary>
	public sealed class ThemeTypeSample(string name, double size, double lineHeight)
	{
		/// <summary>
		/// Gets the step name, for example <c>L</c>.
		/// </summary>
		public string Name { get; } = name;

		/// <summary>
		/// Gets the font size in device independent units.
		/// </summary>
		public double Size { get; } = size;

		/// <summary>
		/// Gets the line height in device independent units.
		/// </summary>
		public double LineHeight { get; } = lineHeight;

		/// <summary>
		/// Gets the size and line height formatted for the right-hand label.
		/// </summary>
		public string Details { get; } = $"{size:0.#} / {lineHeight:0}";

		/// <summary>
		/// Gets the sample text rendered at this size.
		/// </summary>
		public string Sample { get; } = "The quick brown fox";
	}

	/// <summary>
	/// One entry of the corner radius vocabulary.
	/// </summary>
	public sealed class ThemeRadiusSample(string name, CornerRadius radius)
	{
		/// <summary>
		/// Gets the resource key of the radius.
		/// </summary>
		public string Name { get; } = name;

		/// <summary>
		/// Gets the corner radius value.
		/// </summary>
		public CornerRadius Radius { get; } = radius;

		/// <summary>
		/// Gets the value formatted for the label.
		/// </summary>
		public string Details { get; } = radius.TopLeft == radius.TopRight
			&& radius.TopRight == radius.BottomRight
			&& radius.BottomRight == radius.BottomLeft
				? $"{radius.TopLeft:0.#}"
				: $"{radius.TopLeft:0.#} {radius.TopRight:0.#} {radius.BottomRight:0.#} {radius.BottomLeft:0.#}";
	}

	/// <summary>
	/// The theme gallery: every generated rung, the type scale, the radius vocabulary and the controls
	/// that have been taken over so far.
	/// </summary>
	/// <remarks>
	/// Colour and size samples are resolved through explicit variants, so both theme variants can be
	/// inspected at once; role brushes on the page are live <c>DynamicResource</c> references and follow
	/// the variant the application is actually in.
	/// </remarks>
	[ViewModelFor(typeof(ThemeGalleryDebugPageView))]
	public class ThemeGalleryDebugPageViewModel : ViewModelBase
	{
		private static readonly string[] rampSteps =
		[
			"0", "50", "100", "200", "300", "400", "500", "600", "700", "800", "900", "950", "1000",
		];

		private static readonly string[] ramps = ["Neutral", "Primary", "Accent"];
		private static readonly string[] statuses = ["Success", "Warning", "Error", "Danger", "Info"];
		private static readonly string[] typeSteps = ["Xs", "S", "M", "L", "Xl", "2Xl", "3Xl"];
		private static readonly string[] radiusSteps = ["None", "S", "M", "L", "Xl", "Pill", "TopM", "BottomM", "LeftM", "RightM", "InnerM"];

		/// <summary>
		/// Gets the rungs of the light variant.
		/// </summary>
		public RangeObservableCollection<ThemeSwatch> LightRungs { get; } = [];

		/// <summary>
		/// Gets the rungs of the dark variant.
		/// </summary>
		public RangeObservableCollection<ThemeSwatch> DarkRungs { get; } = [];

		/// <summary>
		/// Gets the steps of the type scale.
		/// </summary>
		public RangeObservableCollection<ThemeTypeSample> TypeSamples { get; } = [];

		/// <summary>
		/// Gets the corner radius vocabulary.
		/// </summary>
		public RangeObservableCollection<ThemeRadiusSample> RadiusSamples { get; } = [];

		/// <summary>
		/// Gets the one-line summary of the settings the page was built with.
		/// </summary>
		public string Summary { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="ThemeGalleryDebugPageViewModel"/> class.
		/// </summary>
		public ThemeGalleryDebugPageViewModel()
		{
			var settings = ApplicationStartupSettingsAccessor.Current;
			Summary = $"{settings.ThemeMode} · {settings.FontProfile} · scale {settings.FontScale:0.##} · "
				+ $"radius base {settings.RadiusBase:0.#} · "
				+ $"primary {settings.Primary} · accent {settings.Accent}";

			BuildRungs(LightRungs, ThemeVariant.Light);
			BuildRungs(DarkRungs, ThemeVariant.Dark);
			BuildTypeSamples();
			BuildRadiusSamples();
		}

		private void BuildRungs(RangeObservableCollection<ThemeSwatch> target, ThemeVariant variant)
		{
			foreach (var ramp in ramps)
				foreach (var step in rampSteps)
					AddSwatch(target, ramp + step, variant);

			foreach (var status in statuses)
				AddSwatch(target, "Status" + status + "500", variant);
		}

		private static void AddSwatch(RangeObservableCollection<ThemeSwatch> target, string key, ThemeVariant variant)
		{
			if (FindResource(key, variant) is Color color)
				target.Add(new ThemeSwatch(key, color));
		}

		private void BuildTypeSamples()
		{
			foreach (var step in typeSteps)
			{
				var size = FindResource("FontSize" + step, null) as double? ?? 0;
				var lineHeight = FindResource("LineHeight" + step, null) as double? ?? 0;
				TypeSamples.Add(new ThemeTypeSample(step, size, lineHeight));
			}
		}

		private void BuildRadiusSamples()
		{
			foreach (var step in radiusSteps)
			{
				if (FindResource("Radius" + step, null) is CornerRadius radius)
					RadiusSamples.Add(new ThemeRadiusSample(step, radius));
			}
		}

		private static object? FindResource(string key, ThemeVariant? variant)
			=> Application.Current is { } application && application.TryFindResource(key, variant, out var value)
				? value
				: null;
	}
}
