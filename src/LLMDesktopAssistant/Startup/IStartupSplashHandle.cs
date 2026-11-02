using LLMDesktopAssistant.Localization;

namespace LLMDesktopAssistant.Startup
{
	/// <summary>
	/// The visual side of the startup splash: one implementation per host, because a desktop
	/// window and a single-view control display the same state on different top levels.
	/// Implementations are called from a background thread and must marshal to their own UI thread.
	/// </summary>
	public interface IStartupSplashHandle
	{
		/// <summary>
		/// Announces the total number of stages, so the handle can turn per-stage progress into
		/// a single bar value.
		/// </summary>
		/// <param name="stagesCount">The total number of stages, including the final content step.</param>
		void OnBegin(int stagesCount);

		/// <summary>
		/// Announces a new current stage.
		/// </summary>
		/// <param name="index">The zero-based index of the stage.</param>
		/// <param name="title">The localized stage title.</param>
		void OnStage(int index, LocaleKeyBase title);

		/// <summary>
		/// Reports progress inside the current stage.
		/// </summary>
		/// <param name="value">The stage progress in the range from 0 to 1.</param>
		/// <param name="status">The localized text describing what is happening right now.</param>
		void OnStageProgress(double value, LocaleKeyBase status);

		/// <summary>
		/// Replaces the splash content with the startup error.
		/// </summary>
		/// <param name="exception">The exception that stopped the startup.</param>
		void OnFailed(Exception exception);

		/// <summary>
		/// Asks the splash to fade out and disappear.
		/// </summary>
		void OnCompleted();
	}
}
