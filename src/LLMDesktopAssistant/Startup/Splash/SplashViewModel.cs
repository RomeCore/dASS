using LLMDesktopAssistant.Localization;

namespace LLMDesktopAssistant.Startup.Splash
{
	/// <summary>
	/// The state behind the splash screen. It knows nothing about platforms: the desktop window and
	/// the single-view control both bind to it, and the host decides what to hide.
	/// </summary>
	public class SplashViewModel : ViewModelBase
	{
		private int _stagesCount;
		private int _stageIndex;
		private double _stageProgress;
		private string _stageTitle = string.Empty;
		private string _statusText = string.Empty;
		private string? _errorText;

		/// <summary>
		/// Initializes a new instance of the <see cref="SplashViewModel"/> class.
		/// </summary>
		public SplashViewModel()
		{
			_stageTitle = Locale.Get("splash.loading");
			_statusText = _stageTitle;
		}

		/// <summary>
		/// Gets the application name shown next to the logo.
		/// </summary>
		public string ApplicationName => "dASS";

		/// <summary>
		/// Gets the application version.
		/// </summary>
		public string VersionText => $"v{App.Version}";

		/// <summary>
		/// Gets the title of the stage that is running right now.
		/// </summary>
		public string StageTitle
		{
			get => _stageTitle;
			private set => SetProperty(ref _stageTitle, value);
		}

		/// <summary>
		/// Gets the text describing the current activity inside the stage.
		/// </summary>
		public string StatusText
		{
			get => _statusText;
			private set => SetProperty(ref _statusText, value);
		}

		/// <summary>
		/// Gets the overall progress in the range from 0 to 1.
		/// </summary>
		public double TotalProgress => _stagesCount <= 0 ? 0 : (_stageIndex + _stageProgress) / _stagesCount;

		/// <summary>
		/// Gets the startup error, or <see langword="null"/> while the startup is going well.
		/// </summary>
		public string? ErrorText
		{
			get => _errorText;
			private set
			{
				if (SetProperty(ref _errorText, value))
					RaisePropertyChanged(nameof(HasError));
			}
		}

		/// <summary>
		/// Gets a value indicating whether the splash shows the startup error.
		/// </summary>
		public bool HasError => _errorText is not null;

		/// <summary>
		/// Records the total number of stages.
		/// </summary>
		/// <param name="stagesCount">The total number of stages, including the final content step.</param>
		public void Begin(int stagesCount)
		{
			_stagesCount = stagesCount;
			_stageIndex = 0;
			_stageProgress = 0;
			RaisePropertyChanged(nameof(TotalProgress));
		}

		/// <summary>
		/// Switches the splash to a new stage.
		/// </summary>
		/// <param name="index">The zero-based index of the stage.</param>
		/// <param name="title">The localized stage title.</param>
		public void ApplyStage(int index, LocaleKeyBase title)
		{
			_stageIndex = index;
			_stageProgress = 0;
			StageTitle = title.Value;
			StatusText = title.Value;
			RaisePropertyChanged(nameof(TotalProgress));
		}

		/// <summary>
		/// Updates the progress inside the current stage.
		/// </summary>
		/// <param name="value">The stage progress in the range from 0 to 1.</param>
		/// <param name="status">The localized status text.</param>
		public void ApplyProgress(double value, LocaleKeyBase status)
		{
			_stageProgress = Math.Clamp(value, 0, 1);
			StatusText = status.Value;
			RaisePropertyChanged(nameof(TotalProgress));
		}

		/// <summary>
		/// Replaces the normal content with the startup error.
		/// </summary>
		/// <param name="exception">The exception that stopped the startup.</param>
		public void ShowError(Exception exception)
		{
			StatusText = Locale.Get("splash.failed");
			ErrorText = exception.Message;
		}
	}
}
