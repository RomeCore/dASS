namespace LLMDesktopAssistant.Localization
{
	/// <summary>
	/// Static facade for accessing localized strings by key.
	/// </summary>
	public static class Locale
	{
		/// <summary>
		/// Gets a <see cref="LocaleKey"/> for the specified key. The returned instance is cached,
		/// implements <see cref="System.ComponentModel.INotifyPropertyChanged"/> and updates
		/// <see cref="LocaleKey.Value"/> when the language changes.
		/// </summary>
		/// <param name="key">The full localization key.</param>
		/// <returns>A cached <see cref="LocaleKey"/> instance.</returns>
		public static LocaleKey GetKey(string key)
		{
			return LocaleKey.GetOrCreate(key);
		}

		/// <summary>
		/// Gets a <see cref="ConstLocaleKey"/> for the specified value. The returned instance always returns
		/// the same <paramref name="value"/> regardless of the current language.
		/// </summary>
		/// <param name="value">The value to return.</param>
		/// <returns>The <see cref="ConstLocaleKey"/> instance.</returns>
		public static ConstLocaleKey GetConstKey(string value)
		{
			return new ConstLocaleKey(value);
		}

		/// <summary>
		/// Gets a <see cref="LocaleFormattedKey"/> for the specified key and format arguments: the localized template
		/// rendered with <see cref="string.Format(string, object?[])"/>. The instance is not cached (the arguments are
		/// part of the value), but it caches its own rendered text and refreshes on a language change.
		/// </summary>
		/// <param name="key">The full localization key.</param>
		/// <param name="formatArgs">The format arguments substituted into the localized template.</param>
		/// <returns>The <see cref="LocaleFormattedKey"/> instance.</returns>
		public static LocaleFormattedKey GetFormattedKey(string key, params string?[] formatArgs)
		{
			return new LocaleFormattedKey(key, [.. formatArgs]);
		}

		/// <summary>
		/// Gets the localized value for the specified key. If the key is not found, the key itself is returned.
		/// </summary>
		/// <param name="key">The full localization key.</param>
		/// <returns>The localized string, or the key if it was not found.</returns>
		public static string Get(string key)
		{
			return LocalizationManager.LocalizeStatic(key);
		}

		/// <summary>
		/// Gets the localized value for the specified key and formats it with the given arguments.
		/// </summary>
		/// <param name="key">The full localization key.</param>
		/// <param name="args">The arguments to substitute into the localized format string.</param>
		/// <returns>The formatted localized string, or the key if it was not found.</returns>
		public static string Format(string key, params object?[] args)
		{
			return LocalizationManager.LocalizeStaticFormat(key, args);
		}
	}
}
