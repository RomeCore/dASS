using LLMDesktopAssistant.Settings.Application;
using LLMDesktopAssistant.Startup.Splash;

namespace LLMDesktopAssistant.Avalonia.Android
{
	public class AndroidApp : App
	{
		protected override object? CreateSplashContent()
		{
			if (!ApplicationStartupSettingsAccessor.Current.ShowSplash)
				return null;

			// The same view the desktop window hosts: the compact class drops the wordmark, so
			// the logo fills the screen the way an Android splash is expected to.
			var view = new SplashView { DataContext = new SplashViewModel() };
			view.Classes.Add("compact");

			return view;
		}
	}
}
