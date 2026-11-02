using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;
using LLMDesktopAssistant.Localization;

namespace LLMDesktopAssistant.Startup.Splash
{
	/// <summary>
	/// The splash content itself. It is a plain control so the same markup is usable inside a desktop
	/// window and as the single view of the Android host.
	/// </summary>
	public partial class SplashView : UserControl, IStartupSplashHandle
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="SplashView"/> class.
		/// </summary>
		public SplashView()
		{
			InitializeComponent();
		}

		private SplashViewModel ViewModel => (SplashViewModel)DataContext!;

		/// <inheritdoc />
		public void OnBegin(int stagesCount) => Post(() => ViewModel.Begin(stagesCount));

		/// <inheritdoc />
		public void OnStage(int index, LocaleKeyBase title) => Post(() => ViewModel.ApplyStage(index, title));

		/// <inheritdoc />
		public void OnStageProgress(double value, LocaleKeyBase status) => Post(() => ViewModel.ApplyProgress(value, status));

		/// <inheritdoc />
		public void OnFailed(Exception exception) => Post(() => ViewModel.ShowError(exception));

		/// <inheritdoc />
		public void OnCompleted() => Post(CloseAfterFadeAsync);

		/// <summary>
		/// Every update arrives from a background stage, so the handle - not the caller, not the
		/// manager - owns the marshalling to the UI thread.
		/// </summary>
		private static void Post(Action action) => Dispatcher.UIThread.Post(action);

		/// <summary>
		/// Closes the window that hosts the error screen. The startup has already stopped, so this
		/// is the only way out.
		/// </summary>
		/// <param name="sender">The button that was clicked.</param>
		/// <param name="e">The click arguments.</param>
		private void OnCloseErrorClicked(object? sender, RoutedEventArgs e)
		{
			if (TopLevel.GetTopLevel(this) is Window window)
				window.Close();
		}

		private async void CloseAfterFadeAsync()
		{
			var animation = new Animation
			{
				Duration = TimeSpan.FromMilliseconds(250),
				Easing = new CubicEaseOut(),
				FillMode = FillMode.Forward,
				Children =
				{
					new KeyFrame
					{
						Cue = new Cue(1d),
						Setters = { new Setter(OpacityProperty, 0d) }
					}
				}
			};

			try
			{
				await animation.RunAsync(this);
			}
			catch (Exception)
			{
				// An animation failure must never keep the splash on screen.
			}

			if (TopLevel.GetTopLevel(this) is Window window)
				window.Close();
		}
	}
}
