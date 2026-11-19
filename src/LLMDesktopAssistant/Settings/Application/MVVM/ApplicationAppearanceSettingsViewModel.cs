using Avalonia.Media;
using LLMDesktopAssistant.Styles.Theming;
using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.Settings.Application.MVVM
{
	/// <summary>
	/// ViewModel for the application appearance settings category: the theme variant, the palette seeds,
	/// the type scale and the corner radius base. Every value lives in the startup settings and is applied
	/// through <see cref="ThemeManager"/>, so the splash and the running window always agree.
	/// </summary>
	[ViewModelFor(typeof(ApplicationAppearanceSettingsView))]
	public class ApplicationAppearanceSettingsViewModel : ViewModelBase
	{
		/// <summary>
		/// Gets the theme variants the user can choose from.
		/// </summary>
		public IReadOnlyList<ThemeMode> ThemeModes { get; } = Enum.GetValues<ThemeMode>();

		/// <summary>
		/// Gets the type scale profiles the user can choose from.
		/// </summary>
		public IReadOnlyList<FontProfile> FontProfiles { get; } = Enum.GetValues<FontProfile>();

		/// <summary>
		/// Gets or sets a value indicating whether the startup splash screen is shown.
		/// </summary>
		public bool ShowSplash
		{
			get => ApplicationStartupSettingsAccessor.Current.ShowSplash;
			set
			{
				if (ApplicationStartupSettingsAccessor.Current.ShowSplash == value)
					return;

				ApplicationStartupSettingsAccessor.Current.ShowSplash = value;
				RaisePropertyChanged(nameof(ShowSplash));
			}
		}

		/// <summary>
		/// Gets or sets the theme variant.
		/// </summary>
		public ThemeMode ThemeMode
		{
			get => ThemeManager.Mode;
			set
			{
				if (ThemeManager.Mode == value)
					return;

				ThemeManager.Mode = value;
				RaisePropertyChanged(nameof(ThemeMode));
			}
		}

		/// <summary>
		/// Gets or sets the primary seed colour, shared by both variants.
		/// </summary>
		public Color Primary
		{
			get => ApplicationStartupSettingsAccessor.Current.Primary;
			set
			{
				var settings = ApplicationStartupSettingsAccessor.Current;

				if (settings.Primary == value)
					return;

				ThemeManager.SetSeeds(value, settings.Accent);
				RaisePropertyChanged(nameof(Primary));
			}
		}

		/// <summary>
		/// Gets or sets the accent seed colour, shared by both variants.
		/// </summary>
		public Color Accent
		{
			get => ApplicationStartupSettingsAccessor.Current.Accent;
			set
			{
				var settings = ApplicationStartupSettingsAccessor.Current;

				if (settings.Accent == value)
					return;

				ThemeManager.SetSeeds(settings.Primary, value);
				RaisePropertyChanged(nameof(Accent));
			}
		}

		/// <summary>
		/// Gets or sets the type scale profile.
		/// </summary>
		public FontProfile FontProfile
		{
			get => ApplicationStartupSettingsAccessor.Current.FontProfile;
			set
			{
				if (ApplicationStartupSettingsAccessor.Current.FontProfile == value)
					return;

				ThemeManager.SetFontProfile(value);
				RaisePropertyChanged(nameof(FontProfile));
			}
		}

		/// <summary>
		/// Gets or sets the accessibility multiplier applied on top of the type scale.
		/// </summary>
		public double FontScale
		{
			get => ApplicationStartupSettingsAccessor.Current.FontScale;
			set
			{
				if (Math.Abs(ApplicationStartupSettingsAccessor.Current.FontScale - value) < 0.001)
					return;

				ThemeManager.SetFontScale(value);
				RaisePropertyChanged(nameof(FontScale));
			}
		}

		/// <summary>
		/// Gets or sets the base corner radius, which is the <c>M</c> size of the radius vocabulary.
		/// </summary>
		public double RadiusBase
		{
			get => ApplicationStartupSettingsAccessor.Current.RadiusBase;
			set
			{
				if (Math.Abs(ApplicationStartupSettingsAccessor.Current.RadiusBase - value) < 0.001)
					return;

				ThemeManager.SetRadiusBase(value);
				RaisePropertyChanged(nameof(RadiusBase));
			}
		}
	}
}
