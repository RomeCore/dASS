using LLMDesktopAssistant.Services;

namespace LLMDesktopAssistant.Localization
{
	/// <summary>
	/// Manages localization for the application.
	/// </summary>
	public abstract class LocalizationManager : NotifyPropertyChanged
	{
		private static LocalizationManager? _overrideManager;

		/// <summary>
		/// Sets the manager used by the static localization methods. Intended for the startup
		/// bootstrap and for testing, not for feature code.
		/// </summary>
		/// <param name="manager">The manager to use, or <see langword="null"/> to resolve from the service registry.</param>
		internal static void SetOverrideManager(LocalizationManager? manager)
		{
			_overrideManager = manager;
		}

		/// <summary>
		/// Gets the manager the static localization methods use. It is the override installed during
		/// the shared initialization, or the registered service once the container exists - which is
		/// what lets XAML localize itself before the container is built.
		/// </summary>
		public static LocalizationManager? Current =>
			_overrideManager ?? ServiceRegistry.Provider?.GetService<LocalizationManager>();

		/// <summary>
		/// Event that is raised when the language changes. Subscribers do not need a reference to a manager instance,
		/// which makes it suitable for lightweight objects such as <see cref="LocaleKey"/>.
		/// </summary>
		public static event EventHandler<string>? StaticLanguageChanged;

		/// <summary>
		/// Tries to localize a given key using the current localization manager.
		/// </summary>
		/// <param name="key">The key to localize.</param>
		/// <returns>The localized string, or null if not found.</returns>
		public static string? TryLocalizeStatic(string key)
		{
			return Current?.TryLocalize(key);
		}

		/// <summary>
		/// Localizes a given key using the current localization manager.
		/// </summary>
		/// <param name="key">The key to localize.</param>
		/// <returns>The localized string, or the original key if not found.</returns>
		public static string LocalizeStatic(string key)
		{
			return Current?.TryLocalize(key) ?? key;
		}

		/// <summary>
		/// Localizes a given key using the current localization manager.
		/// </summary>
		/// <param name="formatKey">The key to localize, after localization it will be used as a format string.</param>
		/// <param name="formatArgs">Arguments to be formatted into the localized string.</param>
		/// <returns>The localized string, or the original key if not found.</returns>
		public static string LocalizeStaticFormat(string formatKey, params object?[] formatArgs)
		{
			var format = Current?.Localize(formatKey) ?? formatKey;
			return string.Format(format, formatArgs);
		}

		private string _currentLanguage = string.Empty;
		/// <summary>
		/// Gets or sets the current language as a locale code (for example <c>ru-RU</c>).
		/// An empty string means the neutral (invariant) locale.
		/// </summary>
		public string CurrentLanguage
		{
			get => _currentLanguage;
			set
			{
				if (_currentLanguage != value)
				{
					if (TryChangeLanguage(value))
					{
						SetProperty(ref _currentLanguage, value);
						LanguageChanged?.Invoke(this, _currentLanguage);
						StaticLanguageChanged?.Invoke(null, _currentLanguage);
					}
				}
			}
		}

		/// <summary>
		/// Event that is raised when the language changes.
		/// </summary>
		public event EventHandler<string>? LanguageChanged;

		/// <summary>
		/// Tries to localize a given key to the current language.
		/// </summary>
		/// <param name="key">The key to localize.</param>
		/// <returns>The localized value. If the key is not found, returns null.</returns>
		public abstract string? TryLocalize(string key);

		/// <summary>
		/// Localizes a given key to the current language.
		/// </summary>
		/// <param name="key">The key to localize.</param>
		/// <returns>The localized value. If the key is not found, returns the original key.</returns>
		public string Localize(string key)
		{
			return TryLocalize(key) ?? key;
		}

		/// <summary>
		/// Gets a list of available languages as locale codes (for example <c>ru-RU</c>).
		/// The list contains an empty string for the neutral (invariant) locale.
		/// </summary>
		/// <returns>A list of available locale codes.</returns>
		public abstract IEnumerable<string> GetAvailableLanguages();

		/// <summary>
		/// Tries to change the current language. Languages are locale codes (for example <c>ru-RU</c>),
		/// they are listed in <see cref="GetAvailableLanguages()"/>.
		/// </summary>
		/// <param name="language">The locale code to try and set.</param>
		/// <returns>true if the language was changed; otherwise, false.</returns>
		protected abstract bool TryChangeLanguage(string language);
	}
}
