using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Settings.Application;
using LLMDesktopAssistant.Startup;
using LLMDesktopAssistant.Startup.Splash;
using Serilog;

namespace LLMDesktopAssistant.Desktop
{
	/// <summary>
	/// The desktop splash window: it supplies the frame and forwards every update to the view inside,
	/// which is the same view the Android host uses as its single view.
	/// </summary>
	public partial class SplashWindow : Window, IStartupSplashHandle
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="SplashWindow"/> class.
		/// </summary>
		public SplashWindow()
		{
			InitializeComponent();

			SplashContent.DataContext = new SplashViewModel();

			CenterOnStartupScreen();
		}

		private IStartupSplashHandle Handle => SplashContent;

		/// <inheritdoc />
		public void OnBegin(int stagesCount) => Handle.OnBegin(stagesCount);

		/// <inheritdoc />
		public void OnStage(int index, LocaleKeyBase title) => Handle.OnStage(index, title);

		/// <inheritdoc />
		public void OnStageProgress(double value, LocaleKeyBase status) => Handle.OnStageProgress(value, status);

		/// <inheritdoc />
		public void OnFailed(Exception exception) => Handle.OnFailed(exception);

		/// <inheritdoc />
		public void OnCompleted() => Handle.OnCompleted();

		/// <inheritdoc />
		protected override void OnClosed(EventArgs e)
		{
			// The handle lives exactly as long as the window that draws it.
			StartupSplashManager.Attach(null);
			base.OnClosed(e);
		}

		/// <summary>
		/// Puts the splash in the middle of the screen the application was working on last time.
		/// On a multi-monitor setup the primary screen is often not the one the user is looking at.
		/// </summary>
		private void CenterOnStartupScreen()
		{
			try
			{
				var screens = Screens;
				var screen = GetStartupScreen(screens) ?? screens.Primary;

				if (screen is null)
					return;

				var width = (int)(Width * screen.Scaling);
				var height = (int)(Height * screen.Scaling);
				var bounds = screen.Bounds;

				WindowStartupLocation = WindowStartupLocation.Manual;
				Position = new PixelPoint(
					bounds.X + (bounds.Width - width) / 2,
					bounds.Y + (bounds.Height - height) / 2);
			}
			catch (Exception exception)
			{
				Log.Warning(exception, "Failed to center the splash on the startup screen.");
			}
		}
		
		/// <summary>
		/// Picks the screen the main window closed on. Avalonia exposes no cross-platform cursor
		/// position, and the stored window position is the portable answer: it is where the user was
		/// working, and the main window reopens there anyway.
		/// </summary>
		private static Screen? GetStartupScreen(Screens screens)
		{
			var settings = ApplicationStartupSettingsAccessor.Current;

			// The stored screen origin is the direct answer and survives a maximized run, which leaves
			// no usable window geometry.
			if (settings.WindowScreenX is { } screenX && settings.WindowScreenY is { } screenY)
			{
				// One pixel inside the origin, so a screen starting at a negative coordinate still
				// resolves to itself.
				if (screens.ScreenFromPoint(new PixelPoint(screenX + 1, screenY + 1)) is { } screen)
					return screen;
			}

			if (settings.WindowX is { } x && settings.WindowY is { } y)
				return screens.ScreenFromPoint(new PixelPoint(x, y));

			return null;
		}
	}
}
