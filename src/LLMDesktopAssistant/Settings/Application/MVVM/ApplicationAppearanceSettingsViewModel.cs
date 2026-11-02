using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.Settings.Application.MVVM
{
	/// <summary>
	/// ViewModel for the application appearance settings category. The splash toggle lives in the
	/// startup settings, whose file is readable before the service container exists, so the
	/// bootstrap and this page always agree on the same value.
	/// </summary>
	[ViewModelFor(typeof(ApplicationAppearanceSettingsView))]
	public class ApplicationAppearanceSettingsViewModel : ViewModelBase
	{
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
	}
}
