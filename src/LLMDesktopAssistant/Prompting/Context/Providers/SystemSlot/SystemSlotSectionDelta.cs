using LLMDesktopAssistant.Utils.Files;
using LiteDB;

namespace LLMDesktopAssistant.Prompting.Context.Providers.SystemSlot
{
	/// <summary>
	/// The delta of the system slot section: a unified diff between the previously known text
	/// and the current text of the section.
	/// </summary>
	public class SystemSlotSectionDelta : PromptSectionDeltaBase
	{
		/// <summary>
		/// The hunk groups of the diff (as computed by <see cref="UnifiedDiff.Compute"/>).
		/// </summary>
		public List<HunkGroup> Groups { get; init; } = [];

		/// <summary>
		/// The diff as a <see cref="HunkGroups"/> collection, used for folding and rendering.
		/// </summary>
		[BsonIgnore]
		public HunkGroups Diff => new() { Groups = Groups };
	}
}
