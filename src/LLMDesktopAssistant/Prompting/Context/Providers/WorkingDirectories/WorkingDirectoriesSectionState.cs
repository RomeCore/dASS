namespace LLMDesktopAssistant.Prompting.Context.Providers.WorkingDirectories
{
	/// <summary>
	/// The state of the working directories section: the working directories that are
	/// enabled for the chat, with the active one marked.
	/// Disabled directories are not listed at all: hiding is silent for the section.
	/// </summary>
	public class WorkingDirectoriesSectionState : PromptSectionStateBase
	{
		/// <summary>
		/// The enabled working directories in their configured order.
		/// </summary>
		public List<WorkingDirectoryItem> Items { get; set; } = [];
	}
}
