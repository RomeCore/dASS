using LLMDesktopAssistant.Localization;

namespace LLMDesktopAssistant.Startup
{
	/// <summary>
	/// The static seam between the startup sequence and the splash screen. The stage code publishes
	/// progress through these members, and the host attaches a handle that knows how to draw it.
	/// When no handle is attached every call is a no-op, which is what makes the whole mechanism
	/// free in headless mode.
	/// </summary>
	public static class StartupSplashManager
	{
		private static IStartupSplashHandle? _handle;
		private static int _stagesCount;
		private static int _stageIndex;
		private static double _stageProgress;

		/// <summary>
		/// Gets the total number of stages announced by <see cref="Begin"/>.
		/// </summary>
		public static int StagesCount => _stagesCount;

		/// <summary>
		/// Gets the zero-based index of the current stage.
		/// </summary>
		public static int StageIndex => _stageIndex;

		/// <summary>
		/// Gets the progress inside the current stage, in the range from 0 to 1.
		/// </summary>
		public static double StageProgress => _stageProgress;

		/// <summary>
		/// Gets the overall progress, computed as <c>(stageIndex + stageProgress) / stagesCount</c>.
		/// </summary>
		public static double TotalProgress => _stagesCount <= 0 ? 0 : (_stageIndex + _stageProgress) / _stagesCount;

		/// <summary>
		/// Gets a value indicating whether a visual handle is currently listening. The host uses it
		/// to skip the minimum on-screen time when the splash is disabled.
		/// </summary>
		public static bool IsAttached => _handle is not null;

		/// <summary>
		/// Attaches or detaches the visual handle. Detaching keeps the published state intact, so a
		/// handle attached later still sees the whole picture.
		/// </summary>
		/// <param name="handle">The handle to receive the updates, or <see langword="null"/> to detach.</param>
		public static void Attach(IStartupSplashHandle? handle) => _handle = handle;

		/// <summary>
		/// Starts a new startup sequence with the given number of stages.
		/// </summary>
		/// <param name="stagesCount">The total number of stages, including the final content step.</param>
		public static void Begin(int stagesCount)
		{
			_stagesCount = stagesCount;
			_stageIndex = 0;
			_stageProgress = 0;
			_handle?.OnBegin(stagesCount);
		}

		/// <summary>
		/// Announces a new current stage. The stage progress is reset to zero.
		/// </summary>
		/// <param name="index">The zero-based index of the stage.</param>
		/// <param name="title">The localized stage title.</param>
		public static void SetStage(int index, LocaleKeyBase title)
		{
			_stageIndex = index;
			_stageProgress = 0;
			_handle?.OnStage(index, title);
		}

		/// <summary>
		/// Reports progress inside the current stage.
		/// </summary>
		/// <param name="value">The stage progress in the range from 0 to 1.</param>
		/// <param name="status">The localized text describing what is happening right now.</param>
		public static void SetStageProgress(double value, LocaleKeyBase status)
		{
			_stageProgress = Math.Clamp(value, 0, 1);
			_handle?.OnStageProgress(_stageProgress, status);
		}

		/// <summary>
		/// Reports a startup failure.
		/// </summary>
		/// <param name="exception">The exception that stopped the startup.</param>
		public static void Fail(Exception exception) => _handle?.OnFailed(exception);

		/// <summary>
		/// Reports that the startup is over and the splash may disappear.
		/// </summary>
		public static void Complete() => _handle?.OnCompleted();
	}
}
