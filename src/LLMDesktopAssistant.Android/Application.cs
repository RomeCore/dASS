using Android.App;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;
using LLMDesktopAssistant.Startup;

namespace LLMDesktopAssistant.Avalonia.Android
{
	[Application]
	public class Application : AvaloniaAndroidApplication<AndroidApp>
	{
		protected Application(nint javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
		{
		}

		/// <inheritdoc />
		public override void OnCreate()
		{
			// Directories, logging and localization must exist before the Avalonia application is
			// created, exactly like on the desktop.
			SharedInitialization.Initialize([], AndroidPlatformInitialization.Instance);

			base.OnCreate();
		}

		protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
		{
			return base.CustomizeAppBuilder(builder)
				.WithInterFont();
		}
	}
}
