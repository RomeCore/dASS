namespace LLMDesktopAssistant.Prompting.Context.Providers.WorkingDirectories
{
	/// <summary>
	/// The delta of the working directories section. Every concern is reported by its own field,
	/// so that a delta tells exactly what happened to the working directories.
	/// </summary>
	public class WorkingDirectoriesSectionDelta : PromptSectionDeltaBase
	{
		/// <summary>
		/// Whether the section itself appeared in the prompt: the anchor has no state for it,
		/// because the section was disabled when the anchor was created. In that case
		/// <see cref="Items"/> carries the full list of the working directories.
		/// </summary>
		public bool Appeared { get; set; }

		/// <summary>
		/// The full list of the working directories at the moment the section appeared.
		/// Used by the <see cref="Appeared"/> delta and to reconstruct the known state afterwards.
		/// </summary>
		public List<WorkingDirectoryItem> Items { get; set; } = [];

		/// <summary>
		/// Whether the active working directory switched to another one.
		/// </summary>
		public bool ActiveChanged { get; set; }

		/// <summary>
		/// The active directory before the switch (null when it was not known).
		/// </summary>
		public string? OldActivePath { get; set; }

		/// <summary>
		/// The active directory after the switch.
		/// </summary>
		public string? NewActivePath { get; set; }

		/// <summary>
		/// The working directories that became available.
		/// </summary>
		public List<WorkingDirectoryItem> AddedItems { get; set; } = [];

		/// <summary>
		/// The paths of the working directories that are no longer available.
		/// </summary>
		public List<string> RemovedPaths { get; set; } = [];
	}
}
