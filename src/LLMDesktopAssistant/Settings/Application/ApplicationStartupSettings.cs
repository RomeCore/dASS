using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.Settings.Application
{
	/// <summary>
	/// The handful of settings that must be readable before the service container exists: the splash
	/// toggle, the interface language and the geometry of the main window. They are stored in their own
	/// file and are deliberately not a <see cref="SettingsObject"/>, so that reading them never touches
	/// the polymorphic JSON resolver and therefore never needs the reflection scan, which is a startup stage.
	/// </summary>
	public class ApplicationStartupSettings : NotifyPropertyChanged
	{
		private bool _showSplash = true;
		/// <summary>
		/// Gets or sets a value indicating whether the startup splash screen is shown.
		/// </summary>
		public bool ShowSplash
		{
			get => _showSplash;
			set => SetProperty(ref _showSplash, value);
		}

		private string _language = string.Empty;
		/// <summary>
		/// Gets or sets the interface language as a locale code (for example <c>ru-RU</c>).
		/// An empty string means the neutral (invariant) locale.
		/// </summary>
		public string Language
		{
			get => _language;
			set => SetProperty(ref _language, value);
		}

		private int? _windowX = null;
		/// <summary>
		/// Gets or sets the horizontal position of the main window in physical pixels, or
		/// <see langword="null"/> when it has never been stored.
		/// </summary>
		public int? WindowX
		{
			get => _windowX;
			set => SetProperty(ref _windowX, value);
		}

		private int? _windowY = null;
		/// <summary>
		/// Gets or sets the vertical position of the main window in physical pixels, or
		/// <see langword="null"/> when it has never been stored.
		/// </summary>
		public int? WindowY
		{
			get => _windowY;
			set => SetProperty(ref _windowY, value);
		}

		private int? _windowScreenX = null;
		/// <summary>
		/// Gets or sets the horizontal origin of the screen the main window was on, in physical pixels,
		/// or <see langword="null"/> when it has never been stored. The splash uses it to appear on the
		/// same monitor even after a maximized run, when the window geometry is not stored.
		/// </summary>
		public int? WindowScreenX
		{
			get => _windowScreenX;
			set => SetProperty(ref _windowScreenX, value);
		}

		private int? _windowScreenY = null;
		/// <summary>
		/// Gets or sets the vertical origin of the screen the main window was on, in physical pixels,
		/// or <see langword="null"/> when it has never been stored.
		/// </summary>
		public int? WindowScreenY
		{
			get => _windowScreenY;
			set => SetProperty(ref _windowScreenY, value);
		}

		private double _windowWidth = 0;
		/// <summary>
		/// Gets or sets the width of the main window in device-independent units, or zero when it has
		/// never been stored.
		/// </summary>
		public double WindowWidth
		{
			get => _windowWidth;
			set => SetProperty(ref _windowWidth, value);
		}

		private double _windowHeight = 0;
		/// <summary>
		/// Gets or sets the height of the main window in device-independent units, or zero when it has
		/// never been stored.
		/// </summary>
		public double WindowHeight
		{
			get => _windowHeight;
			set => SetProperty(ref _windowHeight, value);
		}

		private string _windowState = string.Empty;
		/// <summary>
		/// Gets or sets the name of the window state the application was closed in, or an empty string
		/// when it has never been stored.
		/// </summary>
		public string WindowState
		{
			get => _windowState;
			set => SetProperty(ref _windowState, value);
		}
	}
}
