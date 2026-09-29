namespace LLMDesktopAssistant.Prompting.Context.Providers.WorkingDirectories
{
	/// <summary>
	/// The working directories section: tells the agent which folders it may work in
	/// and which of them is the root for relative paths.
	/// </summary>
	public class WorkingDirectoriesSection(IServiceProvider services)
		: PromptAnchoredSectionBase<WorkingDirectoriesSectionState, WorkingDirectoriesSectionDelta>(services)
	{
		public override string Discriminator => "working-directories";
	}
}
