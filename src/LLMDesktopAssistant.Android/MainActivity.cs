using Android.App;
using Android.Content.PM;
using Avalonia.Android;
using LLMDesktopAssistant.Controls.Dialogs;
using LLMDesktopAssistant.MVVM;
using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.Services.Instances;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace LLMDesktopAssistant.Avalonia.Android
{
	[Activity(
		Label = "LLMDesktopAssistant.Avalonia.Android",
		Theme = "@style/MyTheme.NoActionBar",
		Icon = "@drawable/icon",
		MainLauncher = true,
		ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
	public class MainActivity : AvaloniaMainActivity
	{
		public MainActivity()
		{
			Log.Information("MainActivity created. Logging initialized.");

			BackRequested += MainActivity_BackRequested;
		}

		private void MainActivity_BackRequested(object? sender, AndroidBackRequestedEventArgs e)
		{
			if (DialogManager.IsDialogOpen)
			{
				try
				{
					DialogManager.CloseDialog(null);
				}
				catch { }
				e.Handled = true;
			}
		}

		protected override void OnDestroy()
		{
			base.OnDestroy();

			App.MainViewModel?.Dispose();
			foreach (var appClose in ServiceRegistry.Provider.GetServices<IAppClosingHandler>())
				try
				{
					appClose.OnAppClosing();
				}
				catch { }
		}
	}
}
