using Avalonia;
using Avalonia.Controls;
using LLMDesktopAssistant.Settings.Application;
using Serilog;

namespace LLMDesktopAssistant.MVVM
{
	/// <summary>
	/// Restores the main window geometry from the startup settings and stores it again while the window
	/// lives, so the application reopens where the user left it. The screen is captured as soon as the
	/// window appears and on every move: by the time <c>Closing</c> runs the platform window is already
	/// being torn down, and asking it for the screen there is not reliable.
	/// </summary>
	public static class MainWindowPlacement
	{
		/// <summary>
		/// Applies the stored geometry to the window. A position that no longer lands on any screen is
		/// ignored, which is what happens when a monitor was disconnected since the last run.
		/// </summary>
		/// <param name="window">The window to place.</param>
		public static void Restore(Window window)
		{
			try
			{
				var settings = ApplicationStartupSettingsAccessor.Current;

				if (settings.WindowWidth > 0 && settings.WindowHeight > 0)
				{
					window.Width = settings.WindowWidth;
					window.Height = settings.WindowHeight;
				}

				if (Enum.TryParse<WindowState>(settings.WindowState, out var state) && state != WindowState.Minimized)
					window.WindowState = state;

				PixelPoint? position = null;
				if (window.WindowState is WindowState.Maximized &&
					settings.WindowScreenX is { } sx && settings.WindowScreenY is { } sy)
					position = new PixelPoint(sx, sy);
				else if (settings.WindowX is { } x && settings.WindowY is { } y)
					position = new PixelPoint(x, y);
				if (position != null && window.Screens.ScreenFromPoint(position.Value) is not null)
				{
					window.WindowStartupLocation = WindowStartupLocation.Manual;
					window.Position = position.Value;
				}
			}
			catch (Exception exception)
			{
				Log.Warning(exception, "Failed to restore the main window geometry.");
			}
		}

		/// <summary>
		/// Starts recording the screen the window is on and stores the geometry when it closes.
		/// </summary>
		/// <param name="window">The window to track.</param>
		public static void Attach(Window window)
		{
			// A maximized window dragged to another monitor keeps its state, so Windows never reports a
			// position change: activation and state changes are the events that actually fire there.
			window.Opened += (_, _) => CaptureScreen(window);
			window.Activated += (_, _) => CaptureScreen(window);
			window.PositionChanged += (_, _) => CaptureScreen(window);
			window.Closing += (_, _) => Save(window);

			window.PropertyChanged += (_, e) =>
			{
				if (e.Property == Window.WindowStateProperty)
					CaptureScreen(window);
			};
		}

		/// <summary>
		/// Stores the origin of the screen the window is on. It is recorded independently of the window
		/// state, because a maximized run leaves no usable geometry but the splash still has to know
		/// which monitor to appear on.
		/// </summary>
		private static void CaptureScreen(Window window)
		{
			try
			{
				var screen = window.Screens.ScreenFromWindow(window)
					?? window.Screens.ScreenFromPoint(window.Position);
				var settings = ApplicationStartupSettingsAccessor.Current;

				if (screen is null || settings.WindowScreenX == screen.Bounds.X && settings.WindowScreenY == screen.Bounds.Y)
					return;

				settings.WindowScreenX = screen.Bounds.X;
				settings.WindowScreenY = screen.Bounds.Y;

				Log.Debug("Main window screen captured at {X}:{Y}.", screen.Bounds.X, screen.Bounds.Y);
			}
			catch (Exception exception)
			{
				Log.Warning(exception, "Failed to capture the main window screen.");
			}
		}

		/// <summary>
		/// Stores the window state and, while the window is in its normal state, its size and position.
		/// Keeping the normal bounds means a maximized run does not overwrite the size the user chose.
		/// </summary>
		private static void Save(Window window)
		{
			try
			{
				var settings = ApplicationStartupSettingsAccessor.Current;
				settings.WindowState = window.WindowState.ToString();

				CaptureScreen(window);

				if (window.WindowState != WindowState.Normal)
					return;

				settings.WindowX = window.Position.X;
				settings.WindowY = window.Position.Y;
				settings.WindowWidth = window.Bounds.Width;
				settings.WindowHeight = window.Bounds.Height;

				Log.Debug(
					"Main window geometry saved: {X}:{Y} {Width}x{Height}.",
					settings.WindowX,
					settings.WindowY,
					settings.WindowWidth,
					settings.WindowHeight);
			}
			catch (Exception exception)
			{
				Log.Warning(exception, "Failed to store the main window geometry.");
			}
		}
	}
}
