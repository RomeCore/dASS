namespace LLMDesktopAssistant.Settings.Application
{
	/// <summary>
	/// Language settings category of the application.
	/// </summary>
	public class ApplicationLanguageSettings : ApplicationSettingsCategoryBase
	{
		private string? _prompt = null;
		/// <summary>
		/// Gets or sets the language that will be used for retrieving prompts. The value is a locale code
		/// (for example <c>ru-RU</c>), an empty string for the neutral locale, or <c>null</c> for system language.
		/// </summary>
		public string? Prompt
		{
			get => _prompt;
			set => SetProperty(ref _prompt, value);
		}
	}
}
