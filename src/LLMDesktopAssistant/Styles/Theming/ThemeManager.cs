using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using LLMDesktopAssistant.Settings.Application;
using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.Styles.Theming
{
	/// <summary>
	/// The single entry point for switching the theme variant, the palette, the type scale and the radii.
	/// </summary>
	/// <remarks>
	/// The providers own the state: this class holds the references the XAML loader created and forwards
	/// the user's choice to them, so nothing is stored twice. Every setter writes the startup settings
	/// (which are readable before the reflection scan and are what the splash renders with) and then
	/// recomputes the affected provider on the UI thread.
	/// </remarks>
	public static class ThemeManager
	{
		private static readonly List<WeakReference<MarkdownTypographyProvider>> _markdownTypography = [];

		private static PaletteProvider? _palette;
		private static TypographyProvider? _typography;
		private static RadiusProvider? _radius;

		/// <summary>
		/// Gets or sets the requested theme variant and applies it to the application.
		/// </summary>
		public static ThemeMode Mode
		{
			get => ApplicationStartupSettingsAccessor.Current.ThemeMode;
			set => UIDispatcher.Invoke(() =>
			{
				ApplicationStartupSettingsAccessor.Current.ThemeMode = value;
				ApplyMode();
			});
		}

		/// <summary>
		/// Applies the stored theme variant to the application. Called once during startup, before any
		/// window exists, so the splash is already painted in the right variant.
		/// </summary>
		public static void ApplyMode()
		{
			if (Application.Current is not { } application)
				return;

			application.RequestedThemeVariant = ApplicationStartupSettingsAccessor.Current.ThemeMode switch
			{
				ThemeMode.Light => ThemeVariant.Light,
				ThemeMode.Dark => ThemeVariant.Dark,
				_ => ThemeVariant.Default,
			};
		}

		/// <summary>
		/// Replaces the seed colours and regenerates the palette for both variants.
		/// </summary>
		/// <param name="primary">The primary seed.</param>
		/// <param name="accent">The accent seed.</param>
		public static void SetSeeds(Color primary, Color accent)
			=> UIDispatcher.Invoke(() =>
			{
				var settings = ApplicationStartupSettingsAccessor.Current;
				settings.Primary = primary;
				settings.Accent = accent;
				_palette?.Recompute();
			});

		/// <summary>
		/// Selects the type scale profile.
		/// </summary>
		/// <param name="profile">The profile to apply.</param>
		public static void SetFontProfile(FontProfile profile)
			=> UIDispatcher.Invoke(() =>
			{
				ApplicationStartupSettingsAccessor.Current.FontProfile = profile;
				_typography?.Recompute();
				RecomputeMarkdownTypography();
			});

		/// <summary>
		/// Applies an accessibility multiplier on top of the selected profile.
		/// </summary>
		/// <param name="scale">The multiplier, where one means "as designed".</param>
		public static void SetFontScale(double scale)
			=> UIDispatcher.Invoke(() =>
			{
				ApplicationStartupSettingsAccessor.Current.FontScale = Math.Clamp(scale, 0.8, 1.5);
				_typography?.Recompute();
				RecomputeMarkdownTypography();
			});

		/// <summary>
		/// Applies a multiplier on top of the interface scale to Markdown content only.
		/// </summary>
		/// <param name="scale">The multiplier, where one means "the same size as the interface".</param>
		public static void SetMarkdownFontScale(double scale)
			=> UIDispatcher.Invoke(() =>
			{
				ApplicationStartupSettingsAccessor.Current.MarkdownFontScale = Math.Clamp(scale, 0.8, 2);
				RecomputeMarkdownTypography();
			});

		/// <summary>
		/// Sets the base corner radius, which is the <c>M</c> size of the radius vocabulary.
		/// </summary>
		/// <param name="radius">The base radius in device independent units.</param>
		public static void SetRadiusBase(double radius)
			=> UIDispatcher.Invoke(() =>
			{
				ApplicationStartupSettingsAccessor.Current.RadiusBase = Math.Max(0, radius);
				_radius?.Recompute();
			});

		/// <summary>
		/// Registers the palette provider the XAML loader created.
		/// </summary>
		internal static void AttachPalette(PaletteProvider provider) => _palette = provider;

		/// <summary>
		/// Registers the typography provider the XAML loader created.
		/// </summary>
		internal static void AttachTypography(TypographyProvider provider) => _typography = provider;

		/// <summary>
		/// Registers a Markdown scale provider created for one Markdown surface.
		/// </summary>
		/// <remarks>
		/// Markdown surfaces come and go with the messages, so they are tracked weakly: a closed chat must not
		/// keep its provider alive, and a live one must still follow the settings.
		/// </remarks>
		internal static void AttachMarkdownTypography(MarkdownTypographyProvider provider)
		{
			_markdownTypography.RemoveAll(static reference => !reference.TryGetTarget(out _));
			_markdownTypography.Add(new WeakReference<MarkdownTypographyProvider>(provider));
		}

		/// <summary>
		/// Recomputes every live Markdown provider, dropping the dead ones on the way.
		/// </summary>
		private static void RecomputeMarkdownTypography()
		{
			_markdownTypography.RemoveAll(static reference => !reference.TryGetTarget(out _));

			foreach (var reference in _markdownTypography)
			{
				if (reference.TryGetTarget(out var provider))
					provider.Recompute();
			}
		}

		/// <summary>
		/// Registers the radius provider the XAML loader created.
		/// </summary>
		internal static void AttachRadius(RadiusProvider provider) => _radius = provider;
	}
}
