namespace LLMDesktopAssistant.Startup
{
	/// <summary>
	/// Runs the startup stages in order, publishing each one to the splash. The pipeline does not
	/// decide the total number of stages: the host announces it with
	/// <see cref="StartupSplashManager.Begin"/>, because the host adds a step of its own after the
	/// stages are done.
	/// </summary>
	/// <param name="stages">The stages to run, in order.</param>
	/// <param name="stageOffset">The index of the first stage, for a pipeline that does not start at zero.</param>
	public sealed class StartupPipeline(IReadOnlyList<IStartupStage> stages, int stageOffset = 0)
	{
		/// <summary>
		/// Gets the stages this pipeline runs.
		/// </summary>
		public IReadOnlyList<IStartupStage> Stages => stages;

		/// <summary>
		/// Runs every stage on a background thread. A stage that needs the UI thread marshals to the
		/// dispatcher itself, so a slow stage never freezes the splash.
		/// </summary>
		/// <param name="cancellationToken">The token that cancels the startup sequence.</param>
		public async Task RunAsync(CancellationToken cancellationToken = default)
		{
			for (var index = 0; index < stages.Count; index++)
			{
				var stage = stages[index];
				var stageIndex = stageOffset + index;

				StartupSplashManager.SetStage(stageIndex, stage.Title);
				await Task.Run(() => stage.RunAsync(cancellationToken), cancellationToken).ConfigureAwait(false);
				StartupSplashManager.SetStageProgress(1, stage.Title);
			}
		}
	}
}
